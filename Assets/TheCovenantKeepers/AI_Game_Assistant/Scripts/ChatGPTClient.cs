namespace TheCovenantKeepers.AI_Game_Assistant
{
using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Networking;

// Ensure ChatGPTSettings.cs (ScriptableObject definition) is in your project.
// Ensure an instance of ChatGPTSettings is created in a "Resources" folder (e.g., Assets/Resources/ChatGPTSettings.asset)

public static class ChatGPTClient
{
    [System.Serializable]
    public class ChatMessage { public string role; public string content; }
    [System.Serializable]
    public class ChatRequest { public string model; public List<ChatMessage> messages = new List<ChatMessage>(); }
    [System.Serializable]
    public class ChatChoice { public ChatMessage message; }
    [System.Serializable]
    public class ChatResponse { public List<ChatChoice> choices = new List<ChatChoice>(); }

    public static async Task<string> GenerateScriptAsync(string userPrompt, string apiKey, string apiUrl, string model)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("OpenAI API Key is not set. Configure in Window > ChatGPT > OpenAI Settings.");
            return "// Error: API Key not set.";
        }
        if (string.IsNullOrEmpty(userPrompt))
        {
            Debug.LogError("Prompt is empty. Cannot send request to OpenAI.");
            return "// Error: Prompt is empty.";
        }

        // Construct the request payload
        ChatRequest chatRequest = new ChatRequest
        {
            model = model,
            messages = new List<ChatMessage>
            {
                // System message to guide the AI's response format
                new ChatMessage { role = "system", content = "You are an assistant that generates complete, concise, and correct Unity C# MonoBehaviour scripts. Provide only the raw C# code. Do not include any surrounding text, explanations, or markdown formatting like ```csharp or ```. Ensure the class name is suitable for a Unity script, ideally derived from the user's prompt if not specified. Include necessary 'using' directives (like 'using UnityEngine;')." },
                new ChatMessage { role = "user", content = userPrompt }
            }
        };

        string jsonBody = JsonUtility.ToJson(chatRequest);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

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
                Debug.LogError($"OpenAI API Error: {request.error} - Response: {request.downloadHandler.text}");
                return $"// OpenAI API Error: {request.error}\n// Response: {request.downloadHandler.text}";
            }
            else
            {
                string responseJson = request.downloadHandler.text;
                try
                {
                    ChatResponse response = JsonUtility.FromJson<ChatResponse>(responseJson);
                    if (response != null && response.choices != null && response.choices.Count > 0 && response.choices[0].message != null)
                    {
                        string rawContent = response.choices[0].message.content.Trim();
                        // Further clean up to remove potential markdown backticks
                        if (rawContent.StartsWith("```csharp")) rawContent = rawContent.Substring(7);
                        if (rawContent.StartsWith("```")) rawContent = rawContent.Substring(3);
                        if (rawContent.EndsWith("```")) rawContent = rawContent.Substring(0, rawContent.Length - 3);
                        return rawContent.Trim();
                    }
                    else
                    {
                        Debug.LogError("OpenAI API Error: Invalid response format or no choices returned.\nRaw Respons<path_redacted>");
                        return "// OpenAI API Error: Invalid response format. Check console for raw response.";
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("Failed to parse OpenAI JSON response: " + ex.Message + "\nRaw Respons<path_redacted>");
                    return "// OpenAI API Error: Failed to parse JSON response.";
                }
            }
        }
    }

    public static async void TestConnectionAsync() // This matches the call in ChatGPTSettingsEditor
    {
        ChatGPTSettings settings = ChatGPTSettings.Get(); if (settings == null)
        {
            Debug.LogError("ChatGPTSettings asset not found in a 'Resources' folder. Please create it via Window > ChatGPT > OpenAI Settings.");
            return;
        }
        if (string.IsNullOrEmpty(settings.apiKey))
        {
            Debug.LogError("API Key is not set in ChatGPTSettings. Cannot test connection.");
            return;
        }

        Debug.Log($"Attempting to test OpenAI connection with model '{settings.model}'...");
        string testPrompt = "Generate a very simple Unity C# MonoBehaviour script named 'TestConnectionScript' with an empty Start method.";
        string result = await GenerateScriptAsync(testPrompt, settings.apiKey, settings.apiUrl, settings.model);

        if (!string.IsNullOrEmpty(result) && !result.ToLower().Contains("error"))
        {
            Debug.Log("? OpenAI Connection Test Likely Successful! Received a response. Verify if it's valid C# cod<path_redacted>");
        }
        else
        {
            Debug.LogError("? OpenAI Connection Test Failed. Check API Key, URL, Model in settings, and console for detailed errors from OpenAI.");
        }
    }
}
}
