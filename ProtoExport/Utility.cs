namespace GameFrameX.ProtoExport
{
    internal static class Utility
    {
        public static readonly char[] splitChars = { ' ', '\t' };

        public static readonly string[] splitNotesChars = { "//" };

        public static string ConvertType(string type)
        {
            string typeCs = "";
            switch (type)
            {
                case "int16":
                    typeCs = "short";
                    break;
                case "uint16":
                    typeCs = "ushort";
                    break;
                case "int32":
                case "sint32":
                case "sfixed32":
                    typeCs = "int";
                    break;
                case "uint32":
                case "fixed32":
                    typeCs = "uint";
                    break;
                case "int64":
                case "sint64":
                case "sfixed64":
                    typeCs = "long";
                    break;
                case "uint64":
                case "fixed64":
                    typeCs = "ulong";
                    break;
                case "bytes":
                    typeCs = "byte[]";
                    break;
                case "string":
                    typeCs = "string";
                    break;
                case "bool":
                    typeCs = "bool";
                    break;
                case "double":
                    typeCs = "double";
                    break;
                case "float":
                    typeCs = "float";
                    break;
                default:
                    if (type.StartsWith("map<"))
                    {
                        var typeMap = type.Replace("map", string.Empty).Replace("<", string.Empty).Replace(">", string.Empty).Split(',');
                        if (typeMap.Length == 2)
                        {
                            typeCs = $"Dictionary<{ConvertType(typeMap[0])}, {ConvertType(typeMap[1])}>";
                            break;
                        }
                    }

                    typeCs = type;
                    break;
            }

            return typeCs;
        }
        
        public static bool IsBaseType(string type)
        {
            switch (type)
            {
                case "int16":
                case "uint16":
                case "int32":
                case "sint32":
                case "sfixed32":
                case "uint32":
                case "fixed32":
                case "int64":
                case "sint64":
                case "sfixed64":
                case "uint64":
                case "fixed64":
                case "bytes":
                case "string":
                case "bool":
                case "double":
                case "float":
                    return true;
                default:
                    return false;
            }

        }
        
        
        public static string GetBaseTypeMethodName( string type)
        {
            string typeCs = "";

            if (MessageHelper.enum2Module.ContainsKey(type))
            {
                return "Enum";
            }
            
            switch (type)
            {
                /*case "int16":
                    typeCs = "short";
                    break;
                case "uint16":
                    typeCs = "ushort";
                    break;*/
                case "int32":
                case "sint32":
                case "sfixed32":
                    typeCs = "Int32";
                    break;
                case "uint32":
                case "fixed32":
                    typeCs = "UInt32";
                    break;
                case "int64":
                case "sint64":
                case "sfixed64":
                    typeCs = "Int64";
                    break;
                case "uint64":
                case "fixed64":
                    typeCs = "UInt64";
                    break;
                /*case "bytes":
                    typeCs = "byte[]";
                    break;*/
                case "string":
                    typeCs = "String";
                    break;
                case "bool":
                    typeCs = "Bool";
                    break;
                case "double":
                    typeCs = "Double";
                    break;
                case "float":
                    typeCs = "Float";
                    break;
                default:
                    throw new Exception("未处理基础类型" + type);
                    break;
            }

            return typeCs;
        }
    }
}