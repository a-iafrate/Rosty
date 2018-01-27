using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Bot.Connector;
using Microsoft.Bot.Builder.Dialogs;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using SimpleEchoBot.Json;


namespace Microsoft.Bot.Sample.SimpleEchoBot
{
    [Serializable]
    public class EchoDialog : IDialog<object>
    {
        protected int count = 1;

        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);
        }

        public async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> argument)
        {
            var message = await argument;

            //if (message.Text == "reset")
            //{
            //    PromptDialog.Confirm(
            //        context,
            //        AfterResetAsync,
            //        "Are you sure you want to reset the count?",
            //        "Didn't get that!",
            //        promptStyle: PromptStyle.Auto);
            //}
            //else
            //{
            //    await context.PostAsync($"{this.count++}: You said {message.Text}");
            //    context.Wait(MessageReceivedAsync);
            //}

            if (message.Attachments != null && message.Attachments.Any())
            {
                var attachment = message.Attachments.First();
                using (HttpClient httpClient = new HttpClient())
                {
                    // Skype & MS Teams attachment URLs are secured by a JwtToken, so we need to pass the token from our bot.
                    if ((message.ChannelId.Equals("skype", StringComparison.InvariantCultureIgnoreCase) || message.ChannelId.Equals("msteams", StringComparison.InvariantCultureIgnoreCase))
                        && new Uri(attachment.ContentUrl).Host.EndsWith("skype.com"))
                    {
                        var token = await new MicrosoftAppCredentials().GetTokenAsync();
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    }

                    var responseMessage = await httpClient.GetAsync(attachment.ContentUrl);

                    var contentLenghtBytes = responseMessage.Content.Headers.ContentLength;

                   VisionResponse vr= await MakePredictionRequest(await responseMessage.Content.ReadAsByteArrayAsync());

                    string response = "";
                    foreach (var prediction in vr.Predictions)
                    {
                        response += prediction.Tag + ": " + prediction.Probability+" ";
                    }
                    await context.PostAsync(response);
                }
            }
            else
            {
                await context.PostAsync("Mandami una foto e ti dirò quanto sei abbruzzese!");
            }

            context.Wait(this.MessageReceivedAsync);

        }

        static byte[] GetImageAsByteArray(string imageFilePath)
        {
            FileStream fileStream = new FileStream(imageFilePath, FileMode.Open, FileAccess.Read);
            BinaryReader binaryReader = new BinaryReader(fileStream);
            return binaryReader.ReadBytes((int)fileStream.Length);
        }

        static async Task<VisionResponse> MakePredictionRequest(byte[] byteData)
        {
            var client = new HttpClient();

            // Request headers - replace this example key with your valid subscription key.
            client.DefaultRequestHeaders.Add("Prediction-Key", "c48624aef0f349c482b86c13c2561d64");

            // Prediction URL - replace this example URL with your valid prediction URL.
            string url = "https://southcentralus.api.cognitive.microsoft.com/customvision/v1.1/Prediction/7186da44-ab8c-4d12-98ab-346236765c26/image?iterationId=c95bbbe1-7259-41bc-81a1-22ef0898a95c";

            HttpResponseMessage response;

            // Request body. Try this sample with a locally stored image.
            

            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                response = await client.PostAsync(url, content);
                Console.WriteLine(await response.Content.ReadAsStringAsync());
                string ret = await response.Content.ReadAsStringAsync();
               VisionResponse vs= JsonConvert.DeserializeObject<VisionResponse>(ret);
                return vs;
            }
        }


        public async Task AfterResetAsync(IDialogContext context, IAwaitable<bool> argument)
        {
            var confirm = await argument;
            if (confirm)
            {
                this.count = 1;
                await context.PostAsync("Reset count.");
            }
            else
            {
                await context.PostAsync("Did not reset count.");
            }
            context.Wait(MessageReceivedAsync);
        }

    }
}