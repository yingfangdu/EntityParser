using System.Collections.Generic;

namespace EntityParser
{
    internal class Utility
    {
        public static string RefineEntityName(string name)
        {
            List<string> removePatterns = new List<string>()
            {
                "_1__c",
                "_s__c",
                "__c",
            };

            foreach (var pattern in removePatterns)
            {
                name = name.Replace(pattern, string.Empty);
            }

            // change What_can_we_help_with to WhatCanWeHelpWith
            while(true)
            {
                int index = name.IndexOf('_');
                if (index >= 0)
                {
                    string lower = name.Substring(index + 1, 1);
                    string upper = lower.ToUpper();
                    name = name.Substring(0, index) + upper + name.Substring(index + 2);
                }
                else
                {
                    break;
                }
            }

            return name;
        }

        public static string SoapTypeToCSharpTypeMap(string type)
        {
            switch (type)
            {
                case "xsd:dateTime":
                    return "DateTime";
                case "xsd:date":
                    return "DateTime";
                case "tns:ID":
                    return "string";
                case "xsd:boolean":
                    return "bool";
                case "xsd:string":
                    return "string";
                case "xsd:double":
                    return "double";
                default:
                    return "unknown";
            }
        }
    }
}
