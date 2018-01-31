using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Bot.Connector;
using Microsoft.Bot.Builder.Dialogs;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using SimpleEchoBot.Helper;
using SimpleEchoBot.Json;


namespace Microsoft.Bot.Sample.SimpleEchoBot
{
    [Serializable]
    public class ConversationDialog : IDialog<object>
    {
        protected int count = 1;

        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);
        }

        public async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> argument)
        {
            var message = await argument;

            if (!context.PrivateConversationData.ContainsKey("username"))
            {
                //context.PrivateConversationData.SetValue("fistTime",false);
                await context.PostAsync("Ciao, questa è la tua prima volta, inviami una foto e vedrai :D");

                ConversationInfo c=new ConversationInfo();
                c.ToId = message.From.Id;
                c.ToName = message.From.Name;
                c.FromId = message.Recipient.Id;
                c.FromName = message.Recipient.Name;
                c.ServiceUrl = message.ServiceUrl;
                c.ChannelId = message.ChannelId;
                c.ConversationId = message.Conversation.Id;

                PromptDialog.Text(context, AfterResetAsync, "Come posso chiamarti?");
                return;
            }

            String username = context.PrivateConversationData.GetValue<string>("username");

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
                    //foreach (var prediction in vr.Predictions)
                    //{

                    //    response += prediction.Tag + ": " + prediction.Probability+" ";
                    //}
                    VisionResponse.Prediction predictionArrosticini = vr.Predictions.FirstOrDefault(x => x.Tag == "arrosticini");
                    if (predictionArrosticini!=null && predictionArrosticini.Probability > 0.9)
                    {
                        response = "Complimenti, "+username+" ho trovato degli arrosticini";
                    }
                    else
                    {
                        VisionResponse.Prediction predictionPecora = vr.Predictions.FirstOrDefault(x => x.Tag == "pecora");
                        if (predictionPecora!=null && predictionPecora.Probability > 0.9)
                        {
                            response = username+ ", non sono arrosticini ma almeno è una pecora";
                        }
                        else
                        {
                            response =
                                username + ", non vedo arrosticini e nemmeno una pecora, mi dispiace ma non puoi essere abbruzzese!!!";
                        }
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
            string url = "https://southcentralus.api.cognitive.microsoft.com/customvision/v1.1/Prediction/7186da44-ab8c-4d12-98ab-346236765c26/image?iterationId=26d706c5-0b2f-46c3-83b6-3e9d11beaf71";

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


        public async Task AfterResetAsync(IDialogContext context, IAwaitable<string> argument)
        {
            var username = await argument;
            context.PrivateConversationData.SetValue("username", username);

            //Salvo i dati della conversazione


            await context.PostAsync("Ciao "+username +" quando vuoi io sono qui");
            
            
        }

        public async void sendProactiveMessage(ConversationInfo conversation,String messageText)
        {
            // Use the data stored previously to create the required objects.
            var userAccount = new ChannelAccount(conversation.ToId, conversation.ToName);
            var botAccount = new ChannelAccount(conversation.FromId, conversation.FromName);
            var connector = new ConnectorClient(new Uri(conversation.ServiceUrl));

            // Create a new message.
            IMessageActivity message = Activity.CreateMessageActivity();
            if (!string.IsNullOrEmpty(conversation.ConversationId) && !string.IsNullOrEmpty(conversation.ChannelId))
            {
                // If conversation ID and channel ID was stored previously, use it.
                message.ChannelId = conversation.ChannelId;
            }
            else
            {
                // Conversation ID was not stored previously, so create a conversation. 
                // Note: If the user has an existing conversation in a channel, this will likely create a new conversation window.
                conversation.ConversationId = (await connector.Conversations.CreateDirectConversationAsync(botAccount, userAccount)).Id;
            }

            // Set the address-related properties in the message and send the message.
            message.From = botAccount;
            message.Recipient = userAccount;
            message.Conversation = new ConversationAccount(id: conversation.ConversationId);
            message.Text = messageText;
            message.Locale = "en-us";
            await connector.Conversations.SendToConversationAsync((Activity)message);
        }
    }
}