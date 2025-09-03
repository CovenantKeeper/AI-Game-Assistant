namespace TheCovenantKeepers.AI_Game_Assistant
{
// Path: Assets/ChatGPTUnityPlugin/Scripts/CoreLogic/DallEClient.cs
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

public static class DallEClient
{
    // --- Data Structures for DALL-E API JSON ---

    [System.Serializable]
    private class DallERequest
    {
        public string prompt;
        public int n = 1; // Generate 1 image
        public string size = "1024x1024";
        public string response_format = "b64_json"; // Ask for the image data directly
    }

    [System.Serializable]
    private class DallEResponse
    {
        public long created;
        public List<ImageData> data;
    }

    [System.Serializable]
    private class ImageData
    {
        public string b64_json;
    }

    // --- Main Async Method for Image Generation ---

    public static async Task<Texture2D> GenerateImageAsync(string prompt, string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("[DallEClient] OpenAI API Key is not set.");
            return null;
        }

        string apiUrl = "https://api.openai.com/v1/images/generations";

        var requestPayload = new DallERequest
        {
            prompt = prompt
        };

        string jsonBody = JsonUtility.ToJson(requestPayload);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            var asyncOp = request.SendWebRequest();
            while (!asyncOp.isDone)
            {
                await Task.Yield();
            }

#if UNITY_2020_1_OR_NEWER
            if (request.result != UnityWebRequest.Result.Success)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                Debug.LogError($"[DallEClient] API Error: {request.error} - {request.downloadHandler.text}");
                return null;
            }
            else
            {
                try
                {
                    DallEResponse response = JsonUtility.FromJson<DallEResponse>(request.downloadHandler.text);
                    if (response.data != null && response.data.Count > 0)
                    {
                        // Get the base64 encoded image data
                        string base64ImageData = response.data[0].b64_json;
                        // Convert base64 string to byte array
                        byte[] imageData = Convert.FromBase64String(base64ImageData);

                        // Create a new texture and load the image data into it
                        Texture2D texture = new Texture2D(2, 2); // Create a temporary texture
                        if (texture.LoadImage(imageData))
                        {
                            Debug.Log("[DallEClient] Successfully generated and loaded image into Texture2D.");
                            return texture; // Success!
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DallEClient] Failed to process response: {e.Message}");
                    return null;
                }
            }
        }
        return null;
    }
}
}
