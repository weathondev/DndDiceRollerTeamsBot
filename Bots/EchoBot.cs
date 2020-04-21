// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json.Linq;

namespace Microsoft.BotBuilderSamples.Bots
{
    public class EchoBot : TeamsActivityHandler
    {
        private const int CRIT_DIE_TYPE = 20;
        private const int CRIT_HIT_DIE_NUMBER = 20;
        private const int CRIT_FAIL_DIE_NUMBER = 1;

        private static readonly string imageTagStart = @"<img src=""";
        private static readonly string imageTagEnd = @"""></img>";

        private const string INITIATIVE_TO_BE_UPDATED = "initiative Zut+1;Glath+1;Gulrak+2;Durrash-2;Thia+3;Jandar+4;";

        private static readonly List<string> criticalHitImageList = new List<string>
        {
            "https://i.imgur.com/BWobaYD.png",
            "https://i.imgur.com/Wsc2r9l.png",
            "https://i.imgur.com/FVTuKoc.png",
            "https://i.imgur.com/znXNY5K.png",
        };

        private static readonly List<string> criticalFailImageList = new List<string>
        {
            "https://i.imgur.com/eQJZLar.png",
            "https://i.imgur.com/RIpJGPd.png",
            "https://i.imgur.com/iydROxc.png",
            "https://i.imgur.com/96JBEXT.png",
            "https://i.imgur.com/jRCBQiv.png",
            "https://i.imgur.com/I6FAp3A.png",
            "https://i.imgur.com/3eNa3bn.png",
            "https://i.imgur.com/vkBYe5I.png"
        };

        private bool isCriticalHit;
        private bool isCriticalFail;

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            Random random = new Random();
            string replyText = respondToCall(turnContext.Activity.RemoveRecipientMention(), random);
            var replyActivity = MessageFactory.Text($"_{turnContext.Activity.From.Name}_\n\n{replyText}");
            addCritImage(random, replyActivity);
            await turnContext.SendActivityAsync(replyActivity, cancellationToken);
        }

        private string respondToCall(string message, Random random)
        {
            int modifierFinal = -999;
            isCriticalHit = false;
            isCriticalFail = false;
            string replyText =
                "Error, invalid input. Input must be of format '2d6', 'd6', 'adv', 'dis' or '+2' (+2 will be 1d20+2). To all those formats you can add a modifier like '+2' or '-2'" +
                "\n\nInitiative roller e.g. '" + INITIATIVE_TO_BE_UPDATED + "'";

            var messageText = message;
            string parsedMessageText = messageText.ToLowerInvariant().Trim();

            if (parsedMessageText.Contains("init"))
            {
                var initiativeList = getInitiativeList(parsedMessageText, random);

                if (!initiativeList.Any())
                    initiativeList = getInitiativeList(INITIATIVE_TO_BE_UPDATED, random);

                var orderedDictionary = initiativeList.OrderByDescending(x => x.Value);

                if (orderedDictionary.Any())
                    replyText = string.Empty;

                foreach (var pair in orderedDictionary)
                {
                    string modString = pair.Value.mod >= 0 ? "+" + pair.Value.mod : pair.Value.mod + string.Empty;

                    replyText += $"{pair.Key} **{pair.Value.total}** ({modString})\n\n";
                }
            }
            else
            {
                if (parsedMessageText.Contains("+") || parsedMessageText.Contains("-"))
                {
                    int modIndex = parsedMessageText.IndexOf('+') >= 0
                        ? parsedMessageText.IndexOf('+')
                        : parsedMessageText.IndexOf('-');
                    string modifierString = parsedMessageText.Substring(modIndex);
                    int.TryParse(modifierString, out modifierFinal);
                    parsedMessageText = parsedMessageText.Substring(0, modIndex);
                }

                if (parsedMessageText.Contains("adv"))
                {
                    int dieRoll1 = random.Next(1, CRIT_DIE_TYPE + 1);
                    int dieRoll2 = random.Next(1, CRIT_DIE_TYPE + 1);

                    int selectedDieRoll = Math.Max(dieRoll1, dieRoll2);

                    replyText =
                        $"**{(modifierFinal == -999 ? selectedDieRoll : selectedDieRoll + modifierFinal)}** ({dieRoll1}; {dieRoll2};)";

                    setCriticalState(CRIT_DIE_TYPE, selectedDieRoll);
                }
                else if (parsedMessageText.Contains("dis"))
                {
                    int dieRoll1 = random.Next(1, CRIT_DIE_TYPE + 1);
                    int dieRoll2 = random.Next(1, CRIT_DIE_TYPE + 1);

                    int selectedDieRoll = Math.Min(dieRoll1, dieRoll2);

                    replyText =
                        $"**{(modifierFinal == -999 ? selectedDieRoll : selectedDieRoll + modifierFinal)}** ({dieRoll1}; {dieRoll2};)";

                    setCriticalState(CRIT_DIE_TYPE, selectedDieRoll);
                }
                else if (parsedMessageText.Contains("d"))
                {
                    int dIndex = parsedMessageText.IndexOf('d');

                    string numberOfDiceString = parsedMessageText.Substring(0, dIndex);
                    string typeOfNumberString = parsedMessageText.Substring(dIndex + 1);

                    //multi dice roll
                    if (int.TryParse(numberOfDiceString, out int numberOfDice) && numberOfDice > 1)
                    {
                        if (int.TryParse(typeOfNumberString, out int typeOfDice))
                        {
                            int countTogether = 0;
                            string rolledNumbers = string.Empty;

                            for (int i = 0; i < numberOfDice; i++)
                            {
                                int dieRoll = random.Next(1, typeOfDice + 1);
                                rolledNumbers = rolledNumbers + dieRoll + ";";
                                countTogether += dieRoll;

                                setCriticalState(typeOfDice, dieRoll);
                            }

                            replyText =
                                $"**{(modifierFinal == -999 ? countTogether : countTogether + modifierFinal)}** ({rolledNumbers})";
                        }
                    } //just d2, or 1d2
                    else if (int.TryParse(typeOfNumberString, out int typeOfDice))
                    {
                        int dieRoll = random.Next(1, typeOfDice + 1);

                        if (modifierFinal == -999)
                            replyText = $"**{dieRoll}**";
                        else
                            replyText = $"**{dieRoll + modifierFinal}** ({dieRoll})";


                        setCriticalState(typeOfDice, dieRoll);
                    }
                }
                else if (parsedMessageText == string.Empty)
                {
                    int dieRoll1 = random.Next(1, CRIT_DIE_TYPE + 1);

                    if (modifierFinal != -999)
                    {
                        replyText = $"**{dieRoll1 + modifierFinal}**";
                        setCriticalState(CRIT_DIE_TYPE, dieRoll1);
                    }
                }
            }

            //var mention = new Mention
            //{
            //    Mentioned = turnContext.Activity.From,
            //    Text = $"<at>{XmlConvert.EncodeName(turnContext.Activity.From.Name)}</at>"
            //};

            //var replyActivity = MessageFactory.Text($"{mention.Text}\n\n{replyText}");
            //replyActivity.Entities = new List<Entity> {mention};

            return replyText;
        }

        private void addCritImage(Random random, Activity replyActivity)
        {
            string critText = string.Empty;

            if (isCriticalFail)
                critText = "\n\n**FAIL**\n\n" + imageTagStart +
                           criticalFailImageList[random.Next(criticalFailImageList.Count)] + imageTagEnd;
            if (isCriticalHit)
                critText = "\n\n**CRITICAL**\n\n" + imageTagStart +
                           criticalHitImageList[random.Next(criticalHitImageList.Count)] + imageTagEnd;

            if (!string.IsNullOrEmpty(critText))
                replyActivity.Text = replyActivity.Text + critText;
        }

        private static Dictionary<string, (int total, int mod)> getInitiativeList(string parsedMessageText, Random random)
        {
            Dictionary<string, (int total, int mod)> initiativeList = new Dictionary<string, (int total, int mod)>();

            string stripped = parsedMessageText.Replace("initiative", string.Empty);
            string stripped2 = stripped.Replace("init", string.Empty);
            string[] splitted = stripped2.Split(";");

            foreach (string s in splitted)
            {
                if (s.Contains("+") || s.Contains("-"))
                {
                    int modIndex = s.IndexOf('+') > 0 ? s.IndexOf('+') : s.IndexOf('-');
                    string modifierString = s.Substring(modIndex);
                    if (int.TryParse(modifierString, out int mod))
                    {
                        string name = s.Substring(0, modIndex);
                        int dieRoll = random.Next(1, CRIT_DIE_TYPE + 1);

                        initiativeList.Add(name, (dieRoll + mod, mod));
                    }
                }
            }

            return initiativeList;
        }

        private void setCriticalState(int typeOfDice, int dieRoll)
        {
            if (typeOfDice == CRIT_DIE_TYPE)
            {
                if (dieRoll == CRIT_HIT_DIE_NUMBER)
                    isCriticalHit = true;
                else if (dieRoll == CRIT_FAIL_DIE_NUMBER)
                    isCriticalFail = true;
            }
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var welcomeText = "Hello and welcome!";
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
                }
            }
        }

        protected override async Task<MessagingExtensionActionResponse> OnTeamsMessagingExtensionSubmitActionAsync(
            ITurnContext<IInvokeActivity> turnContext, MessagingExtensionAction action, CancellationToken cancellationToken)
        {
            Random random = new Random();
            dynamic data = JObject.Parse(action.Data.ToString());
            string message = (string) data["message"];

            string replyText = respondToCall(message, random);

            var replyActivity = MessageFactory.Text($"_{turnContext.Activity.From.Name} : {message}_\n\n{replyText}");
            addCritImage(random, replyActivity);
            await turnContext.SendActivityAsync(replyActivity, cancellationToken);

            return new MessagingExtensionActionResponse();
        }
    }
}