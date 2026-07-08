using System.Text;
using Microsoft.Extensions.AI;

namespace ServiceDefaults.Extensions;

public static class FunctionCallContentExtensions
{
    public static string AsDisplayString(this FunctionCallContent content)
    {
        StringBuilder stringBuilder = new();
        stringBuilder.Append(content.Name);
        if(content.Arguments?.Any() == true)
        {
            stringBuilder.Append($" (Args: {string.Join(",", content.Arguments.Select(x => $"{x.Key} = {x.Value}"))})");
        }

        return stringBuilder.ToString();
    }
}