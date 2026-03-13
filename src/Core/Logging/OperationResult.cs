using System.Collections.Generic;

namespace TractorGame.Core.Logging
{
    /// <summary>
    /// 统一操作结果：用于携带成功/失败与可追溯的原因码。
    /// </summary>
    public record OperationResult(
        bool Success,
        string? ReasonCode = null,
        Dictionary<string, object?>? Detail = null
    )
    {
        public static readonly OperationResult Ok = new(true);

        public static OperationResult Fail(string reasonCode, Dictionary<string, object?>? detail = null)
        {
            return new OperationResult(false, reasonCode, detail);
        }
    }
}
