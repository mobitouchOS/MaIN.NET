namespace MaIN.Services.Constants;

public static class ServiceConstants
{
    public static class HttpClients
    {
        public const string OpenAiClient = "OpenAiClient";
        public const string GeminiClient = "GeminiClient";
        public const string DeepSeekClient = "DeepSeekClient";
        public const string GroqCloudClient = "GroqCloudClient";
        public const string AnthropicClient = "AnthropicClient";
        public const string XaiClient = "XaiClient";
        public const string OllamaClient = "OllamaClient";
        public const string OllamaLocalClient = "OllamaLocalClient";
        public const string VertexClient = "VertexClient";
        public const string ImageDownloadClient = "ImageDownloadClient";
        public const string ModelContextDownloadClient = "ModelContextDownloadClient";
    }

    public static class ApiUrls
    {
        public const string OpenAiImageGenerations = "https://api.openai.com/v1/images/generations";
        public const string OpenAiChatCompletions = "https://api.openai.com/v1/chat/completions";
        public const string OpenAiResponses = "https://api.openai.com/v1/responses";
        public const string OpenAiSkills = "https://api.openai.com/v1/skills";
        public const string OpenAiModels = "https://api.openai.com/v1/models";

        public const string GeminiImageGenerations =
            "https://generativelanguage.googleapis.com/v1beta/openai/images/generations";

        public const string GeminiOpenAiChatCompletions =
            "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";

        public const string GeminiModels = "https://generativelanguage.googleapis.com/v1beta/models";

        public const string DeepSeekOpenAiChatCompletions = "https://api.deepseek.com/v1/chat/completions";
        public const string DeepSeekModels = "https://api.deepseek.com/models";
        
        public const string GroqCloudOpenAiChatCompletions = "https://api.groq.com/openai/v1/chat/completions";
        public const string GroqCloudModels = "https://api.groq.com/openai/v1/models";

        public const string AnthropicChatMessages = "https://api.anthropic.com/v1/messages";
        public const string AnthropicModels = "https://api.anthropic.com/v1/models";
        public const string AnthropicSkills = "https://api.anthropic.com/v1/skills";

        public const string XaiImageGenerations = "https://api.x.ai/v1/images/generations";
        public const string XaiOpenAiChatCompletions = "https://api.x.ai/v1/chat/completions";
        public const string XaiModels = "https://api.x.ai/v1/models";

        public const string OllamaOpenAiChatCompletions = "https://ollama.com/v1/chat/completions";
        public const string OllamaModels = "https://ollama.com/v1/models";
        
        public const string OllamaLocalOpenAiChatCompletions = "http://localhost:11434/v1/chat/completions";
        public const string OllamaLocalModels = "http://localhost:11434/v1/models";
    }

    public static class Messages
    {
        public const string GeneratedImageContent = "Generated Image:";
        public const string UnprocessedMessage = "Unprocessed";
    }

    public static class Properties
    {
        public const string PreProcessProperty = "Pre_Process";
        public const string DisableCacheProperty = "DisableCache";
        public const string AgentIdProperty = "AgentId";
        public const string MmProjNameProperty = "MmProjName";
        public const string ToolCallsProperty = "ToolCalls";
        public const string ToolCallIdProperty = "ToolCallId";
        public const string ToolNameProperty = "ToolName";
        public const string ClientSideToolExecutionProperty = "ClientSideToolExecution";
        public const string ToolsFormattedProperty = "ToolsFormatted";
    }

    public static class Defaults
    {
        public const string ImageSize = "1024x1024";
    }

    public static class Notifications
    {
        public const string ReceiveMessageUpdate = "ReceiveMessageUpdate";
        public const string ReceiveAgentUpdate = "ReceiveAgentUpdate";

    }

    public static class AnthropicBetaFeatures
    {
        public const string SkillsBetaHeader = "code-execution-2025-08-25,skills-2025-10-02";
        public const string CodeExecutionToolType = "code_execution_20250825";
        public const string CodeExecutionToolName = "code_execution";
        public const string CustomSkillType = "custom";
    }

    public static class OpenAiResponses
    {
        public const string ShellToolType = "shell";
        public const string ContainerAuto = "container_auto";
        public const string SkillReferenceType = "skill_reference";
        public const string FunctionCallType = "function_call";
        public const string FunctionCallOutputType = "function_call_output";
        public const string FunctionToolType = "function";
        public const string DefaultVersionField = "default_version";
    }

    public static class Roles
    {
        public const string Assistant = "assistant";
        public const string User = "user";
        public const string System = "system";
        public const string Tool = "tool";
    }
    
    public static class Grammars
    {
        public const string DecisionGrammar = """
                                              root ::= decision
                                              decision ::= "{" ws "\"decision\":" ws boolean "," ws "\"certainty\":" ws certainty ws "}"
                                              boolean ::= "true" | "false"
                                              certainty ::= "0" | "0." [0-9] [0-9]? | "1" | "1.0" | "1.00"
                                              ws ::= [ \t\n\r]*
                                              """;
        
        public const string KnowledgeGrammar = """
                                               root ::= ws "[" ws (string (ws "," ws string)*)? ws "]"
                                               string ::= "\"" [^"]* "\""
                                               ws ::= [ \t\n\r]*
                                               """;
    }
}