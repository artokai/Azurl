using System.Collections.Generic;
using System.Threading.Tasks;

namespace Azurl.Core
{
    public interface IAliasProvider
    {
        Task<Dictionary<string, string>> Load();
    }
}
