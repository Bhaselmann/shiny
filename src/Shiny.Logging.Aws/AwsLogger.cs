﻿using System;
using Shiny.Logging;


namespace Shiny
{
    public class AwsLogger : ILogger
    {
        public AwsLogger() { }

        public void Write(Exception exception, params (string Key, string Value)[] parameters)
        {
            throw new NotImplementedException();
        }

        public void Write(string eventName, string description, params (string Key, string Value)[] parameters)
        {
            throw new NotImplementedException();
        }
    }
}
