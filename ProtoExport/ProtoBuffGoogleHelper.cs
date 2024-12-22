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
            StringBuilder sb = new StringBuilder();

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

            File.WriteAllText(messageInfoList.OutputPath + ".cs", sb.ToString(), Encoding.UTF8);
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

                // Field number constant
                sb.AppendLine($"\t\tpublic const int {field.Name}FieldNumber = {fieldNumber};");
                
                // Private backing field
                string fieldType = GetFieldType(field);
                string defaultValue = GetDefaultValue(field);
                sb.AppendLine($"\t\tprivate {fieldType} {field.Name.ToCamelCase()}_ {defaultValue};");
                sb.AppendLine();

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
                return $"List<{field.Type}>";
            return field.Type;
        }

        private string GetDefaultValue(MessageMember field)
        {
            if (field.IsRepeated)
                return $"= new List<{field.Type}>()";
            else if (field.Type == "string")
                return "= \"\"";
            else if (field.IsKv)
                return $"= new {field.Type}()";
            return "";
        }

        private void GenerateProperty(StringBuilder sb, MessageMember field)
        {
            sb.AppendLine($"\t\t/// <summary>");
            sb.AppendLine($"\t\t/// {field.Description}");
            sb.AppendLine($"\t\t/// </summary>");
            
            string propertyType = GetFieldType(field);
            string fieldName = field.Name.ToCamelCase();
            
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

        private void GenerateWriteField(StringBuilder sb, MessageMember field, int fieldNumber)
        {
            string fieldName = field.Name.ToCamelCase();
            
            if (field.Type == "string")
            {
                sb.AppendLine($"\t\t\tif ({field.Name}.Length != 0)");
                sb.AppendLine("\t\t\t{");
                sb.AppendLine($"\t\t\t\toutput.WriteRawTag({fieldNumber * 8 + 2}u);");
                sb.AppendLine($"\t\t\t\toutput.WriteString({field.Name});");
                sb.AppendLine("\t\t\t}");
            }
            else if (field.IsRepeated)
            {
                // Add repeated field serialization
            }
            else
            {
                sb.AppendLine($"\t\t\tif ({fieldName}_ != null)");
                sb.AppendLine("\t\t\t{");
                sb.AppendLine($"\t\t\t\toutput.WriteRawTag({fieldNumber * 8 + 2}u);");
                sb.AppendLine($"\t\t\t\toutput.WriteMessage({field.Name});");
                sb.AppendLine("\t\t\t}");
            }
        }

        private void GenerateCalculateSizeMethod(StringBuilder sb, MessageInfo messageInfo)
        {
            sb.AppendLine("\t\tpublic override int CalculateSize()");
            sb.AppendLine("\t\t{");
            sb.AppendLine("\t\t\tint size = 0;");
            
            int fieldNumber = 1;
            foreach (var field in messageInfo.Fields)
            {
                if (!field.IsValid) continue;

                GenerateCalculateFieldSize(sb, field, fieldNumber);
                fieldNumber++;
            }
            
            sb.AppendLine("\t\t\treturn size;");
            sb.AppendLine("\t\t}");
            sb.AppendLine();
        }

        private void GenerateCalculateFieldSize(StringBuilder sb, MessageMember field, int fieldNumber)
        {
            string fieldName = field.Name.ToCamelCase();
            
            if (field.Type == "string")
            {
                sb.AppendLine($"\t\t\tif ({field.Name}.Length != 0)");
                sb.AppendLine("\t\t\t{");
                sb.AppendLine($"\t\t\t\tsize += 1 + CodedOutputStream.ComputeStringSize({field.Name});");
                sb.AppendLine("\t\t\t}");
            }
            else if (field.IsRepeated)
            {
                // Add repeated field size calculation
            }
            else
            {
                sb.AppendLine($"\t\t\tif ({fieldName}_ != null)");
                sb.AppendLine("\t\t\t{");
                sb.AppendLine($"\t\t\t\tsize += 1 + CodedOutputStream.ComputeMessageSize({field.Name});");
                sb.AppendLine("\t\t\t}");
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
            uint tag = (uint)(fieldNumber << 3) | 2;
            
            sb.AppendLine($"\t\t\t\t\tcase {tag}u:");
            if (field.Type == "string")
            {
                sb.AppendLine($"\t\t\t\t\t\t{field.Name} = input.ReadString();");
            }
            else if (field.IsRepeated)
            {
                // Add repeated field merging
            }
            else
            {
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