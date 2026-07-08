namespace ServiceDefaults.Constants;

public static class CosmosDbIds
{
    public const string DatabaseName = "chatbot";

    public static class Containers
    {
        public static class Settings
        {
            public const string ContainerName = "settings";
            public const string PartitionKeyPath = "/userId";
        }

        public static class Conversations
        {
            public const string ContainerName = "conversations";
            public const string PartitionKeyPath = "/userId";
        }
    }
}