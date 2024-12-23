using CommandLine;

namespace GameFrameX.ProtoExport
{
    internal static class Program
    {
        private static IProtoGenerateHelper ProtoGenerateHelper;

        private static int Main(string[] args)
        {
            var launcherOptions = Parser.Default.ParseArguments<LauncherOptions>(args).Value;
            if (launcherOptions == null)
            {
                Console.WriteLine("参数错误，解析失败");
                return 1;
            }

            if (!Enum.TryParse<ModeType>(launcherOptions.Mode, true, out var modeType))
            {
                Console.WriteLine("不支持的运行模式");
                return 1;
            }

            if (!Directory.Exists(launcherOptions.OutputPath))
            {
                Directory.CreateDirectory(launcherOptions.OutputPath);
            }


            var types = typeof(IProtoGenerateHelper).Assembly.GetTypes();
            foreach (var type in types)
            {
                var attrs = type.GetCustomAttributes(typeof(ModeAttribute), true);
                if (attrs?.Length > 0 && (attrs[0] is ModeAttribute modeAttribute) && modeAttribute.Mode == modeType)
                {
                    ProtoGenerateHelper = (IProtoGenerateHelper)Activator.CreateInstance(type);
                    break;
                }
            }

            var files = Directory.GetFiles(launcherOptions.InputPath, "*.proto", SearchOption.AllDirectories);

            var messageInfoLists = new List<MessageInfoList>(files.Length);
            //收集类型
            foreach (var file in files)
            {
                var operationCodeInfo = MessageHelper.Parse(File.ReadAllText(file), Path.GetFileNameWithoutExtension(file), launcherOptions.OutputPath, launcherOptions.IsGenerateErrorCode);
                messageInfoLists.Add(operationCodeInfo);
            }
            
            //生成脚本
            foreach (var operationCodeInfo in messageInfoLists)
            {
                if (!operationCodeInfo.ModuleName.Contains("Proto."))
                {
                    continue;
                }
                Console.WriteLine($"生成: {operationCodeInfo.ModuleName} => Module: {operationCodeInfo.Module} => {operationCodeInfo.OutputPath}");
                switch (modeType)
                {
                    case ModeType.Server:
                        ProtoGenerateHelper?.Run(operationCodeInfo, launcherOptions.OutputPath, launcherOptions.NamespaceName);
                        break;
                    case ModeType.Unity:
                        ProtoGenerateHelper?.Run(operationCodeInfo, launcherOptions.OutputPath, operationCodeInfo.ModuleName);
                        break;
                    case ModeType.TypeScript:
                        ProtoGenerateHelper?.Run(operationCodeInfo, launcherOptions.OutputPath, operationCodeInfo.FileName);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                
               // break;
            }


            ProtoGenerateHelper?.Post(messageInfoLists, launcherOptions.OutputPath);


            Console.WriteLine("导出成功，按任意键退出");
            Console.ReadKey();
            return 0;
        }
    }
}