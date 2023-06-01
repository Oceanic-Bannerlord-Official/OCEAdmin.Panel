using Newtonsoft.Json;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace OCEAdmin.Panel.SteamAPI
{
    public static class SteamAuth
    {
        public static async Task<bool> ValidateSteamTicket(string steamId, string steamTicket)
        {
            string apiKey = Config.Get().SteamAPI;

            using (HttpClient httpClient = new HttpClient())
            {  
                string url = $"http://api.steampowered.com/ISteamUserAuth/AuthenticateUserTicket/v1/?key={apiKey}&appid=261550&ticket={steamTicket}";
                HttpResponseMessage response = await httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();

                    var jsonDocument = JsonDocument.Parse(responseContent);

                    // Access the "result" and "steamid" properties from the JSON
                    string responseResult = jsonDocument.RootElement.GetProperty("response").GetProperty("params").GetProperty("result").GetString();
                    string responseSteamId = jsonDocument.RootElement.GetProperty("response").GetProperty("params").GetProperty("steamid").GetString();

                    // Check if the result is "OK" and the steamid matches the otherSteamId
                    if (responseResult == "OK" && responseSteamId == steamId)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
