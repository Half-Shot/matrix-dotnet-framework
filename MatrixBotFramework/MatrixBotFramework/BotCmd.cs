using System;

namespace MatrixBotFramework
{
    [AttributeUsage(AttributeTargets.Method)]
    public class BotCmd : Attribute{
        public readonly string CMD;
        public readonly string[] BeginsWith;
        public BotCmd(string cmd,params string[] beginswith){
            CMD = cmd;
            BeginsWith = beginswith;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class BotFallback : Attribute {

    }

    [AttributeUsage(AttributeTargets.Method)]
    public class BotHelp : Attribute {
        public readonly string HelpText;
        public BotHelp(string help){
            HelpText = help;
        }
    }

}

