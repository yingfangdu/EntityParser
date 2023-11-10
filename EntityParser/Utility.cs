using System;
using System.Collections.Generic;

namespace EntityParser
{
    internal class Utility
    {
        public static bool TurnOffRefineEntityName = false;
        public static string RefineEntityName(string name)
        {
            if (TurnOffRefineEntityName)
            {
                return name;
            }

            Dictionary<string, string> removePatterns = new Dictionary<string, string>()
            {
                { "_1__c", "C" },
                { "_s__c", "C" },
                { "__c", "C" }
            };

            foreach (var pattern in removePatterns)
            {
                name = name.Replace(pattern.Key, pattern.Value);
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

        public static bool IsCompoundType(string soapType)
        {
            switch(soapType)
            {
                case "urn:address":
                    return true;
                default:
                    return false;
            }
        }

        public static string SoapTypeToCSharpTypeMap(string type)
        {
            switch (type)
            {
                case "xsd:dateTime":
                    return "string";
                case "xsd:date":
                    return "string";
                case "tns:ID":
                    return "string";
                case "xsd:boolean":
                    return "bool";
                case "xsd:string":
                    return "string";
                case "xsd:double":
                    return "double";
                case "xsd:int":
                    return "int";
                case "urn:address":
                default:
                    return "unknown";
            }
        }

        public static string AddNullableMarker(string csharpType, bool isNullable)
        {
            // TODO enable string nullable type.
            if (csharpType == "string")
            {
                return "string";
            }

            return isNullable ? $"{csharpType}?" : csharpType;
        }

        public static string GetSQLType(string csharpType, int precision = 0, int scale = 0)
        {
            if (csharpType != "double" && precision != 0 && scale != 0)
            {
                throw new NotSupportedException("precision and scale are only for double");
            }

            switch (csharpType)
            {
                case "string":
                    return "NVARCHAR(MAX)";
                case "DateTime":
                    return "NVARCHAR(MAX)";
                case "bool":
                    return "NVARCHAR(100)";
                case "int":
                    return "INT";
                case "double":
                    return $"DECIMAL({precision}, {scale})";
                default:
                    return "unknown";
            }
        }
    }
}
