using Microsoft.Extensions.AI;
using MudBlazor;
using ServiceDefaults.Models;

namespace ChatBot.BlazorServerOnly.Models;

public class State
{
    public string? StreamedResponse { get; set; }
    public string? StreamedReasoning { get; set; }
    public List<AIContent> StreamedContents { get; private set; } = [];
    public MemoryUpdate? MemoryUpdate { get; set; }
    public bool IsRecordingAudio { get; set; }
    public bool IsTranscribingAudio { get; set; }
    public bool IsSendingMessage { get; set; }
    public string RecordingStateText => IsTranscribingAudio ? "Transcribing audio" : IsRecordingAudio ? "Stop recording" : "Record audio";
    public string RecordingStateIcon => IsTranscribingAudio ? Icons.Material.Filled.HourglassEmpty : IsRecordingAudio ? Icons.Material.Filled.Stop : Icons.Material.Filled.Mic;
    public Color RecordingStateColor => IsRecordingAudio ? Color.Error : Color.Default;
    public bool DisableRecordingButton => IsSendingMessage || IsTranscribingAudio;

    public void ResetStreamingValues()
    {
        StreamedReasoning = null;
        StreamedResponse = null;
        StreamedContents = [];
    }
}