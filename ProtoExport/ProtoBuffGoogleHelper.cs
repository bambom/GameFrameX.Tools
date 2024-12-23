using System.Text;

namespace GameFrameX.ProtoExport
{
    /// <summary>
    /// 生成ProtoBuf协议文件
    /// </summary>
    [Mode(ModeType.Unity)]
    internal class ProtoBuffGoogleHelper : IProtoGenerateHelper
    {
        public void Run(MessageInfoList messageInfoList, string outputPath, string namespaceName = "Hotfix")
        {
            /*StringBuilder sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using Google.Protobuf;");
            sb.AppendLine("using GameFrameX.Network.Runtime;");
            sb.AppendLine("using System.Diagnostics;");

            messageInfoList.AppendUsings(sb);
            
            sb.AppendLine();
            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");

            foreach (var operationCodeInfo in messageInfoList.Infos)
            {
                if (operationCodeInfo.IsEnum)
                {
                    GenerateEnum(sb, operationCodeInfo);
                }
                else
                {
                    GenerateMessage(sb, operationCodeInfo, messageInfoList.Module);
                }
            }

            sb.Append("}");
            sb.AppendLine();

            File.WriteAllText(messageInfoList.OutputPath + ".cs", sb.ToString(), Encoding.UTF8);*/
            
           
            foreach (MessageInfo operationCodeInfo in messageInfoList.Infos)
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendLine("using System;");
                sb.AppendLine("using Google.Protobuf;");
                sb.AppendLine("using GameFrameX.Network.Runtime;");
                sb.AppendLine("using System.Diagnostics;");
                sb.AppendLine("using Google.Protobuf.Collections;");
                
                messageInfoList.AppendUsings(sb);
            
                sb.AppendLine();
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine("{");
                
                if (operationCodeInfo.IsEnum)
                {
                    GenerateEnum(sb, operationCodeInfo);
                }
                else
                {
                    GenerateMessage(sb, operationCodeInfo, messageInfoList.Module);
                }
                
                sb.Append("}");
                sb.AppendLine();

                if (!Directory.Exists(operationCodeInfo.Root.OutputPath))
                {
                    Directory.CreateDirectory(operationCodeInfo.Root.OutputPath);
                }

                var path = Path.Combine(operationCodeInfo.Root.OutputPath, operationCodeInfo.Name + ".cs");
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                
            }
        }

        private void GenerateEnum(StringBuilder sb, MessageInfo enumInfo)
        {
            sb.AppendLine($"\t/// <summary>");
            sb.AppendLine($"\t/// {enumInfo.Description}");
            sb.AppendLine($"\t/// </summary>");
            sb.AppendLine($"\tpublic enum {enumInfo.Name}");
            sb.AppendLine("\t{");
            
            foreach (var field in enumInfo.Fields)
            {
                sb.AppendLine($"\t\t/// <summary>");
                sb.AppendLine($"\t\t/// {field.Description}");
                sb.AppendLine($"\t\t/// </summary>");
                sb.AppendLine($"\t\t{field.Type} = {field.Members},");
            }

            sb.AppendLine("\t}");
            sb.AppendLine();
        }

        private void GenerateMessage(StringBuilder sb, MessageInfo messageInfo, int module)
        {
            // Class header
            sb.AppendLine($"\t/// <summary>");
            sb.AppendLine($"\t/// {messageInfo.Description}");
            sb.AppendLine($"\t/// </summary>");
            
            if (!string.IsNullOrEmpty(messageInfo.ParentClass))
            {
                sb.AppendLine($"\t[MessageTypeHandler({(module << 16) + messageInfo.Opcode})]");
            }
            
            string inheritance = string.IsNullOrEmpty(messageInfo.ParentClass) 
                ? "" 
                : $" : MessageObject, {messageInfo.ParentClass}";
            
            sb.AppendLine($"\tpublic sealed class {messageInfo.Name}{inheritance}");
            sb.AppendLine("\t{");

            // Parser
            sb.AppendLine($"\t\tprivate static readonly MessageParser<{messageInfo.Name}> _parser = new MessageParser<{messageInfo.Name}>(() => new {messageInfo.Name}());");
            sb.AppendLine();
            sb.AppendLine($"\t\tpublic static MessageParser<{messageInfo.Name}> Parser => _parser;");
            sb.AppendLine();

            // Fields and properties
            int fieldNumber = 1;
            foreach (var field in messageInfo.Fields)
            {
                if (!field.IsValid) continue;

                // 字段序列号
                sb.AppendLine($"\t\tpublic const int {field.Name}FieldNumber = {fieldNumber};");

                if (field.IsRepeated)
                {
                    // 属性字段
                    string fieldType = GetFieldType(field);
                    string staticDefault = GetRepatedStaticDefaultValue(field);
                    sb.AppendLine($"\t\tprivate static readonly FieldCodec<{field.GetNamespaceTypeString()}> _repeated_{field.Name.ToCamelCase()}_codec {staticDefault}");
                    sb.AppendLine();
                    
                    // 属性字段
                    string defaultValue = GetDefaultValue(field);
                    sb.AppendLine($"\t\tprivate readonly {fieldType} {field.Name.ToCamelCase()}_ {defaultValue};");
                    sb.AppendLine();
                }
                else
                {
                    // 属性字段
                    string fieldType = GetFieldType(field);
                    string defaultValue = GetDefaultValue(field);
                    sb.AppendLine($"\t\tprivate  {fieldType} {field.Name.ToCamelCase()}_ {defaultValue};");
                    sb.AppendLine();
                }
               
                

                // Property
                GenerateProperty(sb, field);
                
                fieldNumber++;
            }

            // Serialization methods
            GenerateWriteToMethod(sb, messageInfo);
            GenerateCalculateSizeMethod(sb, messageInfo);
            GenerateMergeFromMethod(sb, messageInfo);

            sb.AppendLine("\t}");
            sb.AppendLine();
        }

        private string GetFieldType(MessageMember field)
        {
            if (field.IsRepeated)
                return $"RepeatedField<{field.GetNamespaceTypeString()}>";
            return field.GetNamespaceTypeString();
        }

        private string GetRepatedStaticDefaultValue(MessageMember field)
        {
            uint wireType = GetWireType(field.Type);
            uint tag = (uint)(field.Members << 3) | wireType;
            return $"= FieldCodec.ForMessage({tag}u, {field.GetNamespaceTypeString()}.Parser);";
        }
        
        private string GetDefaultValue(MessageMember field)
        {
            if (field.IsRepeated)
                return $"= new RepeatedField<{field.GetNamespaceTypeString()}>()";
            else if (field.Type == "string")
                return "= \"\"";
            else if (field.IsKv)
                return $"= new {field.GetNamespaceTypeString()}()";
            return "";
        }

        private void GenerateProperty(StringBuilder sb, MessageMember field)
        {
            sb.AppendLine($"\t\t/// <summary>");
            sb.AppendLine($"\t\t/// {field.Description}");
            sb.AppendLine($"\t\t/// </summary>");
            
            string propertyType = GetFieldType(field);
            string fieldName = field.Name.ToCamelCase();

            if (field.IsRepeated)
            {
                sb.AppendLine($"\t\tpublic {propertyType} {field.Name} => {fieldName}_;");
            }
            else
            {
                sb.AppendLine($"\t\tpublic {propertyType} {field.Name}");
                sb.AppendLine("\t\t{");
                sb.AppendLine($"\t\t\tget {{ return {fieldName}_; }}");
            
                if (field.Type == "string")
                {
                    sb.AppendLine($"\t\t\tset {{ {fieldName}_ = ProtoPreconditions.CheckNotNull(value, \"value\"); }}");
                }
                else
                {
                    sb.AppendLine($"\t\t\tset {{ {fieldName}_ = value; }}");
                }
            
                sb.AppendLine("\t\t}");
            }
            
            sb.AppendLine();
        }

        private void GenerateWriteToMethod(StringBuilder sb, MessageInfo messageInfo)
        {
            sb.AppendLine("\t\tpublic override void WriteTo(CodedOutputStream output)");
            sb.AppendLine("\t\t{");
            
            int fieldNumber = 1;
            foreach (var field in messageInfo.Fields)
            {
                if (!field.IsValid) continue;

                GenerateWriteField(sb, field, fieldNumber);
                fieldNumber++;
            }
            
            sb.AppendLine("\t\t}");
            sb.AppendLine();
        }

        // 主要优化GenerateWriteField方法:
        private void GenerateWriteField(StringBuilder sb, MessageMember field, int fieldNumber)
        {
            string fieldName = field.Name.ToCamelCase();
            // 计算wire type
            uint wireType = GetWireType(field.Type);
            uint tag = (uint)(fieldNumber << 3) | wireType;
        
            if (IsBasicType(field.Type))
            {
                // 基本类型的处理
                string defaultValue = GetDefaultValue(field.Type);
                sb.AppendLine($"\t\t\tif ({fieldName}_ != {defaultValue})");
                sb.AppendLine("\t\t\t{");
                sb.AppendLine($"\t\t\t\toutput.WriteRawTag({tag});");
                sb.AppendLine($"\t\t\t\toutput.Write{GetComputeSizeType(field.OriginType)}({field.Name});");
                sb.AppendLine("\t\t\t}");
            }
            else if (field.IsRepeated) 
            {
                // repeated类型处理
                sb.AppendLine($"\t\t\t{fieldName}_.WriteTo(output, _repeated_{field.Name.ToCamelCase()}_codec);");
            }
            else
            {
                // 复合类型处理
                sb.AppendLine($"\t\t\tif ({fieldName}_ != null)");
                sb.AppendLine("\t\t\t{");
                sb.AppendLine($"\t\t\t\toutput.WriteRawTag({tag});");
                sb.AppendLine($"\t\t\t\toutput.WriteMessage({field.Name});");
                sb.AppendLine("\t\t\t}");
            }
        }

        private void GenerateCalculateSizeMethod(StringBuilder sb, MessageInfo messageInfo)
        {
            sb.AppendLine("\t\tpublic override int CalculateSize()");
            sb.AppendLine("\t\t{");
            sb.AppendLine("\t\t\tint num = 0;");
            
            int fieldNumber = 1;
            foreach (var field in messageInfo.Fields)
            {
                if (!field.IsValid) continue;

                GenerateCalculateFieldSize(sb, field, fieldNumber);
                fieldNumber++;
            }
            
            sb.AppendLine("\t\t\treturn num;");
            sb.AppendLine("\t\t}");
            sb.AppendLine();
        }

        // 优化GenerateCalculateFieldSize方法:
        private void GenerateCalculateFieldSize(StringBuilder sb, MessageMember field, int fieldNumber) 
        {
            string fieldName = field.Name.ToCamelCase();
    
            if (IsBasicType(field.OriginType))
            {
                string defaultValue = GetDefaultValue(field.OriginType); 
                sb.AppendLine($"\t\t\tif ({fieldName}_ != {defaultValue})");
                sb.AppendLine("\t\t\t{"); 
                sb.AppendLine($"\t\t\t num += 1 + CodedOutputStream.Compute{GetComputeSizeType(field.OriginType)}Size({field.Name});");
                sb.AppendLine("\t\t\t}");
            }
            else if (field.IsRepeated)
            {
                // 处理重复字段时，使用相应的计算方法
                sb.AppendLine($"\t\t num += {fieldName}_.CalculateSize(_repeated_{field.Name.ToCamelCase()}_codec);");
            }
            else 
            {
                sb.AppendLine($"\t\t\tif ({fieldName}_ != null)");
                sb.AppendLine("\t\t\t{");

                // 对于消息类型，使用 ComputeMessageSize 来计算
                if (MessageHelper.type2Module.ContainsKey(field.OriginType))
                {
                    sb.AppendLine($"\t\t\t num += 1 + CodedOutputStream.ComputeMessageSize({field.Name});"); 
                }
                else
                {
                    // 处理其他类型字段，选择相应的计算方法
                    sb.AppendLine($"\t\t\t num += 1 + CodedOutputStream.Compute{GetComputeSizeType(field.OriginType)}Size({field.Name});");
                }

                sb.AppendLine("\t\t\t}");
            }
        }

    // 新增帮助方法
    private uint GetWireType(string type)
    {
        switch (type)
        {
            case "int":
            case "long":
            case "uint":
            case "ulong":
            case "int32":
            case "int64":
            case "uint32":
            case "uint64":
            case "bool":
            case "enum":
                return 0; // Varint
            case "string":
            case "bytes":
            case "message":
                return 2; // Length-delimited
            default:
                return 2; // Default to length-delimited
        }
    }
    private string GetComputeSizeType(string type)
    {
        switch (type)
        {
            case "int32":
                return "Int32";
            case "int64":
                return "Int64";
            case "uint32":
                return "UInt32";
            case "uint64":
                return "UInt64";
            case "bool":
                return "Bool";
            case "enum":
                return "Enum";
            case "string":
                return "String";
            case "message":
                return "ComputeMessageSize"; // Length-delimited
            default:
                return "ComputeMessageSize"; // Default to length-delimited
        }
    }
    private bool IsBasicType(string type)
    {
        return type == "int32" || type == "int64" ||
               type == "uint32" || type == "uint64" ||
               type == "bool" || type == "string" ||
               type == "int" || type == "long" || type == "uint" || type == "ulong";

    }

    private string GetDefaultValue(string type)
    {
        switch (type)
        {
            case "int":
            case "long":
            case "uint":
            case "ulong":
                
            case "int32":
            case "int64":
            case "uint32": 
            case "uint64":
                return "0";
            case "bool":
                return "false";
            case "string":
                return "\"\"";
            default:
                return "null";
        }
    }

        private void GenerateMergeFromMethod(StringBuilder sb, MessageInfo messageInfo)
        {
            sb.AppendLine("\t\tpublic override void MergeFrom(CodedInputStream input)");
            sb.AppendLine("\t\t{");
            sb.AppendLine("\t\t\tuint tag;");
            sb.AppendLine("\t\t\twhile ((tag = input.ReadTag()) != 0)");
            sb.AppendLine("\t\t\t{");
            sb.AppendLine("\t\t\t\tswitch (tag)");
            sb.AppendLine("\t\t\t\t{");
            sb.AppendLine("\t\t\t\t\tdefault:");
            sb.AppendLine("\t\t\t\t\t\tinput.SkipLastField();");
            sb.AppendLine("\t\t\t\t\t\tbreak;");
            
            int fieldNumber = 1;
            foreach (var field in messageInfo.Fields)
            {
                if (!field.IsValid) continue;

                GenerateMergeField(sb, field, fieldNumber);
                fieldNumber++;
            }
            
            sb.AppendLine("\t\t\t\t}");
            sb.AppendLine("\t\t\t}");
            sb.AppendLine("\t\t}");
        }

        private void GenerateMergeField(StringBuilder sb, MessageMember field, int fieldNumber)
        {
            string fieldName = field.Name.ToCamelCase();
            uint wireType = GetWireType(field.OriginType);
            uint tag = (uint)(fieldNumber << 3) | wireType;
    
            sb.AppendLine($"\t\t\t\t\tcase {tag}u:");

            if (field.OriginType == "string")
            {
                sb.AppendLine($"\t\t\t\t\t\t{field.Name} = input.ReadString();");
            }
            else if (field.OriginType == "bool")
            {
                sb.AppendLine($"\t\t\t\t\t\t{field.Name} = input.ReadBool();");
            }
            else if (field.OriginType == "int32" || field.OriginType == "int" )
            {
                sb.AppendLine($"\t\t\t\t\t\t{field.Name} = input.ReadInt32();");
            }
            else if (field.OriginType == "int64" || field.OriginType == "long")
            {
                sb.AppendLine($"\t\t\t\t\t\t{field.Name} = input.ReadInt64();");
            }
            else if (field.OriginType == "float")
            {
                sb.AppendLine($"\t\t\t\t\t\t{field.Name} = input.ReadFloat();");
            }
            else if (field.OriginType == "double")
            {
                sb.AppendLine($"\t\t\t\t\t\t{field.Name} = input.ReadDouble();");
            }
            else if (field.OriginType == "uint32" || field.OriginType == "uint")
            {
                sb.AppendLine($"\t\t\t\t\t\t{field.Name} = input.ReadUInt32();");
            }
            else if (field.OriginType == "uint64" || field.OriginType == "ulong")
            {
                sb.AppendLine($"\t\t\t\t\t\t{field.Name} = input.ReadUInt64();");
            }
            else if (field.IsRepeated)
            {
                // Add repeated field merging (handle collections)
                sb.AppendLine($"\t\t\t\t\t\t{field.Name.ToCamelCase()}_.AddEntriesFrom(input, _repeated_{field.Name.ToCamelCase()}_codec);");
            }
            else
            {
                if (!MessageHelper.type2Module.ContainsKey(field.OriginType))
                {
                    Console.WriteLine($"Field type not handled: {field.OriginType}");
                    sb.AppendLine($"Field type not handled: {field.OriginType}");
                }
                
                // Handle nested message types
                sb.AppendLine($"\t\t\t\t\t\tif ({fieldName}_ == null)");
                sb.AppendLine($"\t\t\t\t\t\t\t{fieldName}_ = new {field.Type}();");
                sb.AppendLine($"\t\t\t\t\t\tinput.ReadMessage({fieldName}_);");
            }
            sb.AppendLine("\t\t\t\t\t\tbreak;");
        }

        public void Post(List<MessageInfoList> operationCodeInfo, string launcherOptionsOutputPath)
        {
        }
    }

    public static class StringExtensions
    {
        public static string ToCamelCase(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            return char.ToLowerInvariant(str[0]) + str.Substring(1);
        }
    }
}