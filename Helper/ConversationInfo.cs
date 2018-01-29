using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SimpleEchoBot.Helper
{
    public class ConversationInfo
    {
        public string ToId { get; set; }
        public string ToName { get; set; }
        public string FromId { get; set; }
        public string FromName { get; set; }
        public string ServiceUrl { get; set; }

        public string ChannelId { get; set; }
        public string ConversationId { get; set; }
        
    }
        
    
}