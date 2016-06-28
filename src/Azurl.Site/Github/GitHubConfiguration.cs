using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Azurl.GitHub
{
    public class GitHubConfiguration
    {
        public string Repository { get; set; }
        public string Branch { get; set; } 
        public string HookSecret { get; set; }

    }
}
