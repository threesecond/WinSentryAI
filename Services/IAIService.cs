using WinSentryAI.Models;

namespace WinSentryAI.Services
{
    /// <summary>
    /// AI 提供者抽象介面。MVP 階段僅實作 Gemini，未來可擴充為 OpenAI / Claude / Ollama。
    /// </summary>
    public interface IAIService
    {
        /// <summary>提供者識別名稱（"gemini" 等），會寫入 AnalysisResults.AiModel。</summary>
        string ProviderId { get; }

        /// <summary>實際呼叫所使用的模型名稱（如 "gemini-1.5-flash"），會寫入 AnalysisResults.ModelName。</summary>
        string ModelName { get; }

        /// <summary>API key 是否已正確設定（可呼叫前的快速檢查）。</summary>
        Task<bool> IsConfiguredAsync(CancellationToken ct = default);

        /// <summary>對單一事件做分析。失敗時 IsSuccess=false 且 ErrorMessage 已填，不會丟例外。</summary>
        Task<AIAnalysisResponse> AnalyzeEventAsync(AIAnalysisRequest request, CancellationToken ct = default);

        /// <summary>對單一事件進行多輪對話 follow-up。</summary>
        Task<string> SendChatAsync(string systemPrompt, IList<ChatMessage> chatHistory, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    /// <summary>單次事件分析的輸入。</summary>
    public sealed record AIAnalysisRequest(
        EventRecord TriggerEvent,
        IReadOnlyList<EventRecord> ContextLogs,
        SystemSnapshot? SystemSnapshot,
        string Language // "en" | "zh-TW" | "zh-CN"
    );

    /// <summary>單次事件分析的輸出。Prompt 與 Response 會原樣存進 AnalysisResults。</summary>
    public sealed record AIAnalysisResponse(
        bool IsSuccess,
        string SystemPrompt,
        string UserMessage,
        string? Response,
        string? ErrorMessage,
        string ModelName
    )
    {
        public IReadOnlyDictionary<string, string> RedactionMap { get; init; } =
            new Dictionary<string, string>();
    }
}
