namespace TheCovenantKeepers.AI_Game_Assistant
{
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

public static class GeminiClient
{
    // --- Data Structures for Gemini API JSON Serialization ---

    [System.Serializable]
    private class GeminiRequest
    {
        public List<Content> contents;
    }

    [System.Serializable]
    private class Content
    {
        public List<Part> parts;
    }

    [System.Serializable]
    private class Part
    {
        public string text;
    }

    [System.Serializable]
    private class GeminiResponse
    {
        public List<Candidate> candidates;
        public PromptFeedback promptFeedback;
    }

    [System.Serializable]
    private class Candidate
    {
        public Content content;
    }

    [System.Serializable]
    private class PromptFeedback
    {
        public List<SafetyRating> safetyRatings;
    }

    [System.Serializable]
    private class SafetyRating
    {
        public string category;
        public string probability;
    }


    // --- Main Async Method for Script Generation ---

    public static async Task<string> GenerateScriptAsync(string userPrompt, string apiKey, string modelName = "gemini-1.5-flash")
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("Gemini API Key is not set. Configure in Window > ChatGPT > AI Provider Settings.");
            return "// Error: Gemini API Key not set.";
        }
        if (string.IsNullOrEmpty(userPrompt))
        {
            Debug.LogError("Prompt is empty. Cannot send request to Gemini.");
            return "// Error: Prompt is empty.";
        }

        // Gemini API endpoint structure
        string apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

        // Construct the request payload in the format Gemini expects
        var requestPayload = new GeminiRequest
        {
            contents = new List<Content>
            {
                new Content
                {
                    parts = new List<Part>
                    {
                        // The system prompt and user prompt are combined here.
                        new Part { text = "You are an assistant that generates complete, concise, and correct Unity C# MonoBehaviour scripts. Provide only the raw C# code. Do not include any surrounding text, explanations, or markdown formatting like ```csharp or ```. Ensure the class name is suitable for a Unity script, ideally derived from the user's prompt if not specified. Include necessary 'using' directives." },
                        new Part { text = $"User Prompt: {userPrompt}"}
                    }
                }
            }
        };

        string jsonBody = JsonUtility.ToJson(requestPayload);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            var asyncOp = request.SendWebRequest();
            while (!asyncOp.isDone)
            {
                await Task.Yield(); // Wait for the request to complete
            }

#if UNITY_2020_1_OR_NEWER
            if (request.result != UnityWebRequest.Result.Success)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                Debug.LogError($"Gemini API Error: {request.error} - Response: {request.downloadHandler.text}");
                return $"// Gemini API Error: {request.error}\n// Response: {request.downloadHandler.text}";
            }
            else
            {
                string responseJson = request.downloadHandler.text;
                try
                {
                    GeminiResponse response = JsonUtility.FromJson<GeminiResponse>(responseJson);

                    // Check if response was blocked for safety reasons
                    if (response.candidates == null || response.candidates.Count == 0)
                    {
                        if (response.promptFeedback != null && response.promptFeedback.safetyRatings != null)
                        {
                            foreach (var rating in response.promptFeedback.safetyRatings)
                            {
                                if (rating.probability != "NEGLIGIBLE" && rating.probability != "LOW")
                                {
                                    Debug.LogError($"Gemini API Error: Request was blocked due to safety settings. Category: {rating.category}, Probability: {rating.probability}.");
                                    return $"// Gemini Error: Content blocked for safety. Category: {rating.category}";
                                }
                            }
                        }
                        Debug.LogError("Gemini API Error: Invalid response format, no candidates returned.\nRaw Response<path_redacted>");
                        return "// Gemini API Error: Invalid response format. Check console.";
                    }

                    // Extract the text from the response
                    string rawContent = response.candidates[0].content.parts[0].text.Trim();

                    // Further clean up to remove potential markdown backticks
                    if (rawContent.StartsWith("```csharp")) rawContent = rawContent.Substring(7);
                    if (rawContent.StartsWith("```")) rawContent = rawContent.Substring(3);
                    if (rawContent.EndsWith("```")) rawContent = rawContent.Substring(0, rawContent.Length - 3);

                    return rawContent.Trim();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("Failed to parse Gemini JSON response: " + ex.Message + "\nRaw Respons<path_redacted>");
                    return "// Gemini API Error: Failed to parse JSON response.";
                }
            }
        }
    }

    public static async void TestConnectionAsync()
    {
        ChatGPTSettings settings = ChatGPTSettings.Get(); // Using the safe loader from your settings script
        if (settings == null)
        {
            Debug.LogError("Could not load settings asset. Cannot test Gemini connection.");
            return;
        }
        if (string.IsNullOrEmpty(settings.geminiApiKey))
        {
            Debug.LogError("Gemini API Key is not set in settings. Cannot test connection.");
            return;
        }

        Debug.Log("Attempting to test Gemini connection...");
        string testPrompt = "Generate a simple Unity C# MonoBehaviour script named 'GeminiConnectionTest' with an empty Start method.";
        string result = await GenerateScriptAsync(testPrompt, settings.geminiApiKey);

        if (!string.IsNullOrEmpty(result) && !result.ToLower().Contains("error"))
        {
            Debug.Log("? Gemini Connection Test Likely Successful! Received a response. Verify if it's valid C# cod<path_redacted>");
        }
        else
        {
            Debug.LogError("? Gemini Connection Test Failed. Check your Gemini API Key in settings and console for detailed errors.");
        }
    }
}
}
