﻿using Newtonsoft.Json;
using System.IO;

namespace Cactus_Reader.Sources.ToolKits
{
    public class DBProperties
    {
        readonly static string DB_PROPERTY = @"
        {
            'Server=': '127.0.0.1',
            ';Port=': '3306',
            ';User ID=': 'root',
            ';Password=': '123456',
            ';Database=': 'cactus_reader',
            ';Charset=': 'GBK',
            ';SslMode=': 'none',
            ';Min Pool Size=': '1',
            ';Max Pool Size=': '5'
        }";

        public static string GetDatabase()
        {
            string dbConnString = "";
            JsonTextReader jsonTextReader = new JsonTextReader(new StringReader(DB_PROPERTY));
            while (jsonTextReader.Read())
            {
                if (null != jsonTextReader.Value)
                {
                    dbConnString += jsonTextReader.Value;
                }
            }
            return dbConnString;
        }
    }
}