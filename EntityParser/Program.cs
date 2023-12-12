namespace EntityParser
{
    using System;

    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 4)
            {
                Console.WriteLine("args[0]: the entity describe file");
                Console.WriteLine("args[1]: the entity sample file");
                Console.WriteLine("args[2]: the out put folder");
                Console.WriteLine("args[3]: the entity name");
                return;
            }

            string describeFilePath = args[0];// @"C:\Users\yingfand\OneDrive - Microsoft\Temp\UCM\Xandr\Response\account-describe.json";
            string sampleFilePath = args[1];// @"C:\Users\yingfand\OneDrive - Microsoft\Temp\UCM\Xandr\Response\account-sample.json";
            string outPutFilePath = args[2];// @"C:\Users\yingfand\Download\Ouput";
            string entityName = args[3];// "Account";
            var parser = new Parser(describeFilePath, sampleFilePath, outPutFilePath, entityName);
            parser.Process();
        }
    }
}
