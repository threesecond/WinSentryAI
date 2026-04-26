using System.Threading;
using System.Threading.Tasks;
using WinSentryAI.Models;

namespace WinSentryAI.Services
{
    public interface ISystemSnapshotService
    {
        /// <summary>
        /// 收集當前系統環境快照資訊
        /// </summary>
        Task<SystemSnapshot> CollectAsync(CancellationToken ct = default);
    }
}
