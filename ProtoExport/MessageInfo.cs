using System.Text;
using System.Text.RegularExpressions;

namespace GameFrameX.ProtoExport
{
    public class MessageInfoList
    {
        /// <summary>
        /// 消息模块ID
        /// </summary>
        public short Module { get; set; }

        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// 模块名
        /// </summary>
        public string ModuleName { get; set; }

        /// <summary>
        /// 消息列表
        /// </summary>
        public List<MessageInfo> Infos { get; set; } = new List<MessageInfo>(32);

        /// <summary>
        /// 输出路径
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// 当前模块引用的其他模块名称集合
        /// </summary>
        private readonly HashSet<string> ReferencedModules = new();

        /// <summary>
        /// 初始化时自动添加基础引用
        /// </summary>
        public MessageInfoList()
        {
            // 添加基础引用
            AddReferencedModule("GameFrameX.Network.Runtime");
            AddReferencedModule("System");
        }

        /// <summary>
        /// 添加引用模块，避免重复添加和自引用
        /// </summary>
        public void AddReferencedModule(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName) || moduleName == ModuleName)
            {
                return;
            }

            ReferencedModules.Add(moduleName);
        }

        /// <summary>
        /// 自动分析并添加所有消息中引用的模块
        /// </summary>
        public void AnalyzeAndAddReferences()
        {
            foreach (var info in Infos)
            {
                // 分析消息字段中的类型引用
                foreach (var field in info.Fields)
                {
                    if (!field.IsValid) continue;

                    if (MessageHelper.type2Module.TryGetValue(field.Type, out var module))
                    {
                        AddReferencedModule(module);
                    }
                }
            }
        }

        /// <summary>
        /// 获取所有引用的模块名称
        /// </summary>
        public IReadOnlyCollection<string> GetReferencedModules() => ReferencedModules;

        /// <summary>
        /// 添加所需的using语句
        /// </summary>
        public void AppendUsings(StringBuilder sb)
        {
            // 确保在添加using之前已经分析了所有引用
            AnalyzeAndAddReferences();

            // 添加其他模块的using，排除System因为已经单独处理
            foreach (var referencedModule in ReferencedModules.OrderBy(x => x))
            {
                if (referencedModule != "System")
                {
                    sb.AppendLine($"using {referencedModule};");
                }
            }

            // 添加空行分隔
            sb.AppendLine();
        }
    }

    /// <summary>
    /// 消息码信息
    /// </summary>
    /// <summary>
    /// 消息码信息
    /// </summary>
    public class MessageInfo
    {
        
        private string _name;
        private readonly MessageInfoList _root;

        public MessageInfoList Root => _root;

        /// <summary>
        /// 所属的Proto文件路径
        /// </summary>
        public string ProtoFilePath { get; set; }

        /// <summary>
        /// 引用的消息类型列表
        /// </summary>
        private readonly HashSet<string> _referencedTypes = new HashSet<string>();

        public MessageInfo(MessageInfoList root, bool isEnum = false)
        {
            _root = root;
            IsEnum = isEnum;
            Fields = new List<MessageMember>();
            Description = string.Empty;
            
           
        }

        /// <summary>
        /// 是否是请求
        /// </summary>
        public bool IsRequest { get; private set; }

        /// <summary>
        /// 是否是响应
        /// </summary>
        public bool IsResponse { get; private set; }

        /// <summary>
        /// 是否是通知
        /// </summary>
        public bool IsNotify { get; private set; }

        /// <summary>
        /// 是否是心跳
        /// </summary>
        public bool IsHeartbeat { get; private set; }

        /// <summary>
        /// 是否是消息
        /// </summary>
        public bool IsMessage => IsRequest || IsResponse || IsNotify;

        /// <summary>
        /// 消息名称，用于请求和响应配对
        /// </summary>
        public string MessageName { get; private set; }

        /// <summary>
        /// 父类
        /// </summary>
        public string ParentClass
        {
            get
            {
                if (IsEnum)
                {
                    return string.Empty;
                }

                string parentClass = string.Empty;
                if (IsRequest)
                {
                    parentClass = "IRequestMessage";
                }
                else if (IsNotify)
                {
                    parentClass = "INotifyMessage";
                }
                else if (IsResponse)
                {
                    parentClass = "IResponseMessage";
                }

                if (IsHeartbeat && !string.IsNullOrEmpty(parentClass))
                {
                    parentClass += ", IHeartBeatMessage";
                }

                return parentClass;
            }
        }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                _name = value;

                // 解析消息类型
                IsRequest = _name.StartsWith("Req") || _name.StartsWith("C2S_") || _name.EndsWith("Request");
                IsNotify = _name.StartsWith("Notify");
                IsHeartbeat = _name.Contains("Heartbeat", StringComparison.OrdinalIgnoreCase);
                IsResponse = _name.StartsWith("Resp") || _name.StartsWith("S2C_") || _name.EndsWith("Response") ||
                             IsNotify || (IsHeartbeat && !IsRequest);

                // 设置消息名称
                if (IsRequest)
                {
                    if (_name.StartsWith("Req"))
                    {
                        MessageName = _name[3..];
                    }
                    else if (_name.StartsWith("C2S_"))
                    {
                        MessageName = _name[4..];
                    }
                }
                else
                {
                    if (_name.StartsWith("Resp") || _name.StartsWith("S2C_"))
                    {
                        MessageName = _name[4..];
                    }
                }

                // 解析类型引用
                ParseTypeReferences();
            }
        }

        /// <summary>
        /// 解析类型引用
        /// </summary>
        private void ParseTypeReferences()
        {
            if (Fields == null) return;

            foreach (var field in Fields)
            {
                if (string.IsNullOrEmpty(field.Type)) continue;

                // 解析字段类型是否引用了其他模块
                var typeInfo = ParseTypeInfo(field.Type);
                if (!string.IsNullOrEmpty(typeInfo.ModuleName))
                {
                    _root.AddReferencedModule(typeInfo.ModuleName);
                    _referencedTypes.Add(field.Type);
                }
            }

            // 解析父类引用
            var parentClassValue = ParentClass;
            if (!string.IsNullOrEmpty(parentClassValue))
            {
                var parentTypeInfo = ParseTypeInfo(parentClassValue);
                if (!string.IsNullOrEmpty(parentTypeInfo.ModuleName))
                {
                    _root.AddReferencedModule(parentTypeInfo.ModuleName);
                    _referencedTypes.Add(parentClassValue);
                }
            }
        }

        private (string ModuleName, string TypeName) ParseTypeInfo(string fullTypeName)
        {
            // 假设类型格式为: ModuleName.TypeName 或 TypeName
            var parts = fullTypeName.Split('.');
            if (parts.Length > 1)
            {
                return (parts[0], parts[1]);
            }

            return (string.Empty, parts[0]);
        }

        /// <summary>
        /// 获取引用的类型列表
        /// </summary>
        public IReadOnlyCollection<string> GetReferencedTypes() => _referencedTypes;

        /// <summary>
        /// 操作码
        /// </summary>
        public int Opcode { get; set; }

        /// <summary>
        /// 是否是枚举
        /// </summary>
        public bool IsEnum { get; set; }

        /// <summary>
        /// 字段
        /// </summary>
        public List<MessageMember> Fields { get; set; }

        /// <summary>
        /// 注释
        /// </summary>
        public string Description { get; set; }
    }

    public class MessageMember
    {
        private int _members;

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid
        {
            get { return !string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(Type); }
        }

        /// <summary>
        /// 字段类型
        /// </summary>
        public string Type { get; set; }
        //proto原始类型
        public string OriginType { get; set; }
        
        public string GetNamespaceTypeString()
        {
           MessageHelper. type2Module.TryGetValue(Type, out var space);
           if (!string.IsNullOrEmpty(space))
           {
               return space + "." + Type;
           }
           return Type;
        }
        
        public string GetNamespaceTypeString(string targetType)
        {
            MessageHelper. type2Module.TryGetValue(targetType, out var space);
            if (!string.IsNullOrEmpty(space))
            {
                return space + "." + targetType;
            }
            return targetType;
        }

        /// <summary>
        /// 注释
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 成员编码
        /// </summary>
        public int Members
        {
            get => _members;
            set
            {
                _members = value;
                if (value >= 800 && Name != "ErrorCode")
                {
                    //throw new Exception("成员编码不能大于800");
                    _members = 600;
                }
            }
        }

        /// <summary>
        /// 是否是重复
        /// </summary>
        public bool IsRepeated { get; set; }

        /// <summary>
        /// 是否是kv键值对
        /// </summary>
        public bool IsKv { get; set; }

        /// <summary>
        /// 是否是枚举
        /// </summary>
        public bool IsEnum()
        {
            return MessageHelper.enum2Module.ContainsKey(Type);
        }
        
        public (string, string) GetMapTypeSpace()
        {
            string pattern = @"map\s*<\s*(\w+)\s*,\s*(\w+)\s*>";
        
            Match match = Regex.Match(OriginType, pattern);
            if (match.Success)
            {
                string keyType = match.Groups[1].Value;
                string valueType = match.Groups[2].Value;
            
                return (GetNamespaceTypeString(keyType),GetNamespaceTypeString(valueType));
            }
            else
            {
                Console.WriteLine("No match found.");
                throw new Exception("解析map异常");
            }
            
        }
        public (string, string) GetMapType()
        {
            string pattern = @"map\s*<\s*(\w+)\s*,\s*(\w+)\s*>";
        
            Match match = Regex.Match(OriginType, pattern);
            if (match.Success)
            {
                string keyType = match.Groups[1].Value;
                string valueType = match.Groups[2].Value;
            
                return (keyType,valueType);
            }
            else
            {
                Console.WriteLine("No match found.");
                throw new Exception("解析map异常");
            }
            
        }
        public (string, string) GetMapTypeConvert()
        {
            string pattern = @"map\s*<\s*(\w+)\s*,\s*(\w+)\s*>";
        
            Match match = Regex.Match(OriginType, pattern);
            if (match.Success)
            {
                string keyType = match.Groups[1].Value;
                string valueType = match.Groups[2].Value;

                keyType = Utility.ConvertType(keyType);
                valueType = Utility.ConvertType(valueType);
                return (GetNamespaceTypeString(keyType),GetNamespaceTypeString(valueType));
            }
            else
            {
                Console.WriteLine("No match found.");
                throw new Exception("解析map异常");
            }
            
        }
    }
}