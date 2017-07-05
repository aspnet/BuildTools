using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace RepoTasks
{
    public class SayHello : Task
    {
        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "Aloha");
            return true;
        }
    }
}
