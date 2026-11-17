using System.Collections.Generic;
using BUTR.MessageBoxPInvoke.Helpers;
using NLog;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CustomSpawns.Utils
{
    public class MessageBoxService
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, int> _numberOfTimesShown = new();

        public void ShowCustomSpawnsErrorMessage(System.Exception? e, string during = "")
        {
            string duringMessage = "";
            string errorMessage = "";
            if (!string.IsNullOrWhiteSpace(during))
            {
                duringMessage = "DURING " + during + " ";
            }
            if (e != null)
            {
                errorMessage = e.Message + " AT " + e.Source + " " + duringMessage + "TRACE: " + e.StackTrace;
            }
            string shown = new TextObject("{=SpawnAPIErr001}CustomSpawns error has occured, please report to mod developer: ").ToString() + errorMessage;
            Logger.Error(e, "Custom Spawns error " + duringMessage);
            Show(shown);
        }

        public virtual void ShowMessage(string errorMessage)
        {
            Logger.Error(errorMessage);
            Show(errorMessage);
        }

        private void Show(string errorMessage)
        {
            if (!_numberOfTimesShown.ContainsKey(errorMessage))
            {
                _numberOfTimesShown.Add(errorMessage, 1);
            }
            else
            {
                _numberOfTimesShown[errorMessage]++;
            }

            if (_numberOfTimesShown[errorMessage] > 1)
            {
                UX.ShowMessage(errorMessage, Color.ConvertStringToColor(UX.GetMessageColour("error")));
            }
            else
            {
                MessageBoxDialog.Show(errorMessage);
            }
        }

    }
}
