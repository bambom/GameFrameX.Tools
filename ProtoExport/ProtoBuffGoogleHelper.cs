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

                var folder = operationCodeInfo.Root.OutputPath.Replace("Proto.","");
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                var path = Path.Combine(folder, operationCodeInfo.Name + ".cs");
                Console.WriteLine($"写入：{path}");
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

            bool isMsg = !string.IsNullOrEmpty(messageInfo.ParentClass);
            string inheritance = isMsg
                ? $" : MessageObject, {messageInfo.ParentClass}"
                : " : Google.Protobuf.IMessage";
                

            sb.AppendLine($"\tpublic sealed class {messageInfo.Name}{inheritance}");
            sb.AppendLine("\t{");

            // Parser
            sb.AppendLine(
                $"\t\tprivate static readonly MessageParser<{messageInfo.Name}> _parser = new MessageParser<{messageInfo.Name}>(() => new {messageInfo.Name}());");
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
                    string fieldType = GetFieldType(field);
                    string staticDefault = GetRepatedStaticDefaultValue(field);
                    sb.AppendLine(
                        $"\t\tprivate static readonly FieldCodec<{field.GetNamespaceTypeString()}> _repeated_{field.Name.ToCamelCase()}_codec {staticDefault}");

                    sb.AppendLine();

                    // 属性字段
                    string defaultValue = GetDefaultValue(field);
                    sb.AppendLine($"\t\tprivate readonly {fieldType} {field.Name.ToCamelCase()}_ {defaultValue};");
                    sb.AppendLine();
                }
                else if(field.IsKv)
                {
                    var mapType = field.GetMapTypeConvert();
                    string staticDefault = GetMapStaticDefaultValue(field);
                    sb.AppendLine(
                        $"\t\tprivate static readonly MapField<{mapType.Item1},{mapType.Item2}>.Codec _map_{field.Name.ToCamelCase()}_codec {staticDefault}");

                    sb.AppendLine();
                    
                    sb.AppendLine($"\t\tprivate readonly MapField<{mapType.Item1},{mapType.Item2}> {field.Name.ToCamelCase()}_ =" +
                                  $" new MapField<{mapType.Item1},{mapType.Item2}>();");
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
            GenerateWriteToMethod(sb, messageInfo,isMsg);
            GenerateCalculateSizeMethod(sb, messageInfo,isMsg);
            GenerateMergeFromMethod(sb, messageInfo,isMsg);

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
            uint wireType = GetWireType(field.Type,field.IsRepeated);
            uint tag = (uint)(field.Members << 3) | wireType;
            if (Utility.IsBaseType(field.OriginType))
            {
                return $"= FieldCodec.For{Utility.GetBaseTypeMethodName(field.OriginType)}({tag}u);";
            }
            else
            {
                return $"= FieldCodec.ForMessage({tag}u, {field.GetNamespaceTypeString()}.Parser);";
            }
           
        }
        
        private string GetMapStaticDefaultValue(MessageMember field)
        {
            //map类型de tag
            uint wireType = GetWireType(field.Type,field.IsRepeated);
            uint maptag = (uint)(field.Members << 3) | wireType;
            
            var mapType = field.GetMapType();
            var mapTypeSpace = field.GetMapTypeSpace();
            
            //key value 的tag
            uint keytag = (uint)(1 << 3) | GetWireType(mapType.Item1,false);
            uint valuetag = (uint)(2 << 3) | GetWireType(mapType.Item2,false);;

            string keyCode = "";
            //key 1
            if (Utility.IsBaseType(mapType.Item1))
            {
                keyCode =  $"FieldCodec.For{Utility.GetBaseTypeMethodName(mapTypeSpace.Item1)}({keytag}u)";
            }
            else
            {
                keyCode =  $"FieldCodec.ForMessage({keytag}u,{mapTypeSpace.Item1}.Parser)";
            }
            
            string ValueCode = "";
            //value
            if (Utility.IsBaseType(mapType.Item2))
            {
                ValueCode =  $"FieldCodec.For{Utility.GetBaseTypeMethodName(mapTypeSpace.Item2)}({valuetag}u)";
            }
            else
            {
                ValueCode =  $"FieldCodec.ForMessage({valuetag}u,{mapTypeSpace.Item2}.Parser)";
            }

            var mapTypeSpaceConvert = field.GetMapTypeConvert();
            
            return
                $"= new MapField<{mapTypeSpaceConvert.Item1}, {mapTypeSpaceConvert.Item2}>.Codec({keyCode},{ValueCode}, {maptag}u);";
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
            else if (field.IsKv)
            {
                var mapType = field.GetMapTypeConvert();
                sb.AppendLine($"\t\tpublic MapField<{mapType.Item1},{mapType.Item2}> {field.Name} => {fieldName}_;");
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

        private void GenerateWriteToMethod(StringBuilder sb, MessageInfo messageInfo,bool isInheritance)
        {
            if (isInheritance)
            {
                sb.AppendLine("\t\tpublic override void WriteTo(CodedOutputStream output)");
            }
            else
            {
                sb.AppendLine("\t\tpublic void WriteTo(CodedOutputStream output)");
            }
           
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
        private void WriteTag(uint tag, StringBuilder sb)
        {
            // 将 tag 转换为多个字节
            List<byte> bytes = new List<byte>();
            while (tag >= 128)
            {
                bytes.Add((byte)(tag | 0x80));  // 将最高位设为1，表示后面还有字节
                tag >>= 7;  // 将 tag 右移 7 位
            }
            bytes.Add((byte)tag);  // 最后一个字节不需要设置最高位

            // 根据拆分的字节数，调用不同重载的 WriteRawTag 方法
            switch (bytes.Count)
            {
                case 1:
                    sb.AppendLine($"\t\t\t\toutput.WriteRawTag({bytes[0]});");
                    break;
                case 2:
                    sb.AppendLine($"\t\t\t\toutput.WriteRawTag({bytes[0]}, {bytes[1]});");
                    break;
                case 3:
                    sb.AppendLine($"\t\t\t\toutput.WriteRawTag({bytes[0]}, {bytes[1]}, {bytes[2]});");
                    break;
                case 4:
                    sb.AppendLine($"\t\t\t\toutput.WriteRawTag({bytes[0]}, {bytes[1]}, {bytes[2]}, {bytes[3]});");
                    break;
                case 5:
                    sb.AppendLine($"\t\t\t\toutput.WriteRawTag({bytes[0]}, {bytes[1]}, {bytes[2]}, {bytes[3]}, {bytes[4]});");
                    break;
                default:
                    throw new InvalidOperationException("Too many bytes for tag.");
            }
        }
        private void GenerateWriteField(StringBuilder sb, MessageMember field, int fieldNumber)
        {
            string fieldName = field.Name.ToCamelCase();
            // 计算wire type
            uint wireType = GetWireType(field.Type,field.IsRepeated);
            uint tag = (uint)(fieldNumber << 3) | wireType;
           
            
            
            if (!field.IsRepeated && field.OriginType.Equals("string"))
            {
                string defaultValue = GetDefaultValue(field.Type);
                sb.AppendLine($"\t\t\tif ({field.Name}.Length != {defaultValue})");
                sb.AppendLine("\t\t\t{");
               // sb.AppendLine($"\t\t\t\toutput.WriteRawTag({tag});");
                WriteTag(tag, sb);
                sb.AppendLine($"\t\t\t\toutput.Write{Utility.GetBaseTypeMethodName(field.OriginType)}({field.Name});");
                sb.AppendLine("\t\t\t}");
            }
            else if (!field.IsRepeated &&  Utility.IsBaseType(field.OriginType))
            {
                // 基本类型的处理
                string defaultValue = GetDefaultValue(field.Type);
                sb.AppendLine($"\t\t\tif ({field.Name} != {defaultValue})");
                sb.AppendLine("\t\t\t{");
                //sb.AppendLine($"\t\t\t\toutput.WriteRawTag({tag});");
                WriteTag(tag, sb);
                sb.AppendLine($"\t\t\t\toutput.Write{Utility.GetBaseTypeMethodName(field.OriginType)}({field.Name});");
                sb.AppendLine("\t\t\t}");
            }
            else if (field.IsRepeated)
            {
                // repeated类型处理
                sb.AppendLine($"\t\t\t{fieldName}_.WriteTo(output, _repeated_{field.Name.ToCamelCase()}_codec);");
            }
            else if (field.IsKv)
            {
                // map类型处理
                sb.AppendLine($"\t\t\t{fieldName}_.WriteTo(output, _map_{field.Name.ToCamelCase()}_codec);");
            }
            else if (field.IsEnum())
            {
                sb.AppendLine($"\t\t\tif ({field.Name} != 0)");
                sb.AppendLine("\t\t\t{");
               // sb.AppendLine($"\t\t\t\toutput.WriteRawTag({tag});");
                WriteTag(tag, sb);
                sb.AppendLine($"\t\t\t\toutput.WriteEnum((int){field.Name});");
                sb.AppendLine("\t\t\t}");
            }
            else
            {
                // 复合类型处理
                sb.AppendLine($"\t\t\tif ({fieldName}_ != null)");
                sb.AppendLine("\t\t\t{");
                //sb.AppendLine($"\t\t\t\toutput.WriteRawTag({tag});");
                WriteTag(tag, sb);
                sb.AppendLine($"\t\t\t\toutput.WriteMessage({field.Name});");
                sb.AppendLine("\t\t\t}");
            }
        }

        private void GenerateCalculateSizeMethod(StringBuilder sb, MessageInfo messageInfo,bool isInheritance)
        {
            if (isInheritance)
            {
                sb.AppendLine("\t\tpublic override int CalculateSize()"); 
            }
            else
            {
                sb.AppendLine("\t\tpublic int CalculateSize()");
            }
            
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

        // 优化
        private void GenerateCalculateFieldSize(StringBuilder sb, MessageMember field, int fieldNumber)
        {
           int GetTagSize(int num)
            {
                if (num <= 15) return 1;      // 1-15 范围使用 1 字节
                if (num <= 2047) return 2;    // 16-2047 范围使用 2 字节
                if (num <= 262143) return 3;  // 2048-262143 范围使用 3 字节
                return 4;
            }

            int tagSize = GetTagSize(fieldNumber);
            string fieldName = field.Name.ToCamelCase();

            if (!field.IsRepeated && field.OriginType.Equals("string") )
            {
                string defaultValue = GetDefaultValue(field.OriginType);
                sb.AppendLine($"\t\t\tif ({field.Name}.Length != 0)");
                sb.AppendLine("\t\t\t{");
                sb.AppendLine(
                    $"\t\t\t\tnum += {tagSize} + CodedOutputStream.ComputeStringSize({field.Name});");
                sb.AppendLine("\t\t\t}");
            }
           else if (!field.IsRepeated && Utility.IsBaseType(field.OriginType) )
            {
                string defaultValue = GetDefaultValue(field.OriginType);
                sb.AppendLine($"\t\t\tif ({field.Name} != {defaultValue})");
                sb.AppendLine("\t\t\t{");
                sb.AppendLine(
                    $"\t\t\t\tnum += {tagSize} + CodedOutputStream.Compute{Utility.GetBaseTypeMethodName(field.OriginType)}Size({field.Name});");
                sb.AppendLine("\t\t\t}");
            }
            else if (field.IsRepeated)
            {
                // 处理重复字段时，使用相应的计算方法
                sb.AppendLine($"\t\t\tnum += {fieldName}_.CalculateSize(_repeated_{field.Name.ToCamelCase()}_codec);");
            }
            else if (field.IsKv)
            {
                // 处理重复字段时，使用相应的计算方法
                sb.AppendLine($"\t\t\tnum += {fieldName}_.CalculateSize(_map_{field.Name.ToCamelCase()}_codec);");
            }
            else if (field.IsEnum())
            {
                sb.AppendLine($"\t\t\tif ({fieldName}_ != 0)");
                sb.AppendLine("\t\t\t{");
                sb.AppendLine(
                    $"\t\t\t\tnum += {tagSize}+ CodedOutputStream.ComputeEnumSize((int){field.Name});");
                sb.AppendLine("\t\t\t}");
            }
            else
            {
                sb.AppendLine($"\t\t\tif ({fieldName}_ != null)");
                sb.AppendLine("\t\t\t{");

                // 对于消息类型，使用 ComputeMessageSize 来计算
                if (MessageHelper.type2Module.ContainsKey(field.OriginType))
                {
                    sb.AppendLine($"\t\t\t\tnum += {tagSize} + CodedOutputStream.ComputeMessageSize({field.Name});");
                }
                else
                {
                    // 处理其他类型字段，选择相应的计算方法
                    sb.AppendLine(
                        $"\t\t\t\tnum += {tagSize} + CodedOutputStream.Compute{Utility.GetBaseTypeMethodName(field.OriginType)}Size({field.Name});");
                }

                sb.AppendLine("\t\t\t}");
            }
        }
      
       /*
        wireType
            Protobuf 的序列化类型信息，用来表示数据的编码方式。每种类型都有对应的 wire type：
            0 (Varint)：用于整型（如 int32、int64、bool）。
            1 (Fixed64)：用于 fixed64、double。
            2 (Length-delimited)：用于字符串、消息、字节数组，以及 packed 的 repeated 类型。
            5 (Fixed32)：用于 fixed32、float。
            对于 repeated int32 GetRewardIds：
            非 packed 格式使用 wireType = 0（因为是 int32 类型的 Varint 编码）。
            packed 格式使用 wireType = 2（因为是 length-delimited 编码的字节数组）。
        * 
        */
       private uint GetWireType(string type, bool isRepeated)
       {
           // 如果是 repeated 类型，优先使用 packed 格式（Length-delimited）
           if (isRepeated)
           {
               return 2; // Packed repeated fields use Length-delimited
           }

           if (type.StartsWith("map<"))
           {
               return 2; // Packed repeated fields use Length-delimited
           }
           
           // 非 repeated 类型按类型处理
           switch (type)
           {
               case "int":
               case "int32":
               case "int64":
               case "uint":
               case "uint32":
               case "uint64":
               case "long":
               case "ulong":
               case "bool":
               case "enum":
                   return 0; // Varint

               case "fixed32":
               case "sfixed32":
               case "float":
                   return 5; // Fixed32

               case "fixed64":
               case "sfixed64":
               case "double":
                   return 1; // Fixed64

               case "string":
               case "bytes":
               case "message":
                   return 2; // Length-delimited

               default:
                   return 2;
                   //throw new ArgumentException($"Unknown type: {type}");
           }
       }

        private string GetDefaultValue(string type)
        {
            switch (type)
            {
                case "int":
                case "uint32":
                case "uint":
                case "int32":
                    return "0";
                case "long":
                case "int64":
                case "ulong":
                case "uint64":
                    return "0L";
                case "bool":
                    return "false";
                case "string":
                    return "0"; //string判断长度
                default:
                    return "null";
            }
        }

        private void GenerateMergeFromMethod(StringBuilder sb, MessageInfo messageInfo,bool isInheritance)
        {
            if (isInheritance)
            {
                sb.AppendLine("\t\tpublic override void MergeFrom(CodedInputStream input)");
            }
            else
            {
                sb.AppendLine("\t\tpublic void MergeFrom(CodedInputStream input)");
            }
           
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
            uint wireType = GetWireType(field.OriginType,field.IsRepeated);
            uint tag = (uint)(fieldNumber << 3) | wireType;

            //如果是基础类型int之类 需要支持package和非pacjage
            if (field.IsRepeated && Utility.IsBaseType(field.OriginType))
            {
                uint packagetag = (uint)(fieldNumber << 3) | 2;
                uint unpackagetag = (uint)(fieldNumber << 3) | 0;
                sb.AppendLine($"\t\t\t\t\tcase {unpackagetag}u:");
                sb.AppendLine($"\t\t\t\t\tcase {packagetag}u:");
            }
            else
            {
                sb.AppendLine($"\t\t\t\t\tcase {tag}u:");
            }
           
            
            if (!field.IsRepeated && Utility.IsBaseType(field.OriginType))
            {
                sb.AppendLine($"\t\t\t\t\t\t{field.Name} = input.Read{Utility.GetBaseTypeMethodName(field.OriginType)}();");
            }
            else if (field.IsRepeated)
            {
                // Add repeated field merging (handle collections)
                sb.AppendLine(
                    $"\t\t\t\t\t\t{field.Name.ToCamelCase()}_.AddEntriesFrom(input, _repeated_{field.Name.ToCamelCase()}_codec);");
            }
            else if (field.IsKv)
            {
                sb.AppendLine(
                    $"\t\t\t\t\t\t{field.Name.ToCamelCase()}_.AddEntriesFrom(input, _map_{field.Name.ToCamelCase()}_codec);");
            }
            else if(field.IsEnum())
            {
                sb.AppendLine($"\t\t\t\t\t\t{field.Name} = ({field.Type})input.ReadEnum();");
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
                sb.AppendLine($"\t\t\t\t\t\t{{");
                sb.AppendLine($"\t\t\t\t\t\t\t{fieldName}_ = new {field.Type}();");
                sb.AppendLine($"\t\t\t\t\t\t}}");
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