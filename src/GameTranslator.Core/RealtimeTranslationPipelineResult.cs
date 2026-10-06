namespace GameTranslator.Core;

public sealed record RealtimeTranslationPipelineResult(
    TranslationPipelineResult PipelineResult,
    bool TextChanged);
