import { useEffect, useRef, useState } from "react";
import { Button, Textarea } from "@fluentui/react-components";
import {
  Attach20Regular,
  Dismiss12Regular,
  Mic20Regular,
  Send20Filled,
  Stop20Filled,
} from "@fluentui/react-icons";

interface ComposerProps {
  text: string;
  files: File[];
  disabled: boolean;
  allowAttachments: boolean;
  allowAudio: boolean;
  onTextChange: (text: string) => void;
  onFilesChange: (files: File[]) => void;
  onSend: () => void;
  onTranscribe: (audio: Blob, fileName: string) => Promise<string>;
  onError: (message: string) => void;
}

interface FilePreview {
  file: File;
  url?: string;
}

export function Composer({
  text,
  files,
  disabled,
  allowAttachments,
  allowAudio,
  onTextChange,
  onFilesChange,
  onSend,
  onTranscribe,
  onError,
}: ComposerProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const recorderRef = useRef<MediaRecorder | null>(null);
  const mediaStreamRef = useRef<MediaStream | null>(null);
  const audioChunksRef = useRef<Blob[]>([]);
  const [previews, setPreviews] = useState<FilePreview[]>([]);
  const [isRecording, setIsRecording] = useState(false);
  const [isTranscribing, setIsTranscribing] = useState(false);

  useEffect(() => {
    const nextPreviews = files.map((file) => ({
      file,
      url: file.type.startsWith("image/") ? URL.createObjectURL(file) : undefined,
    }));
    setPreviews(nextPreviews);
    return () => nextPreviews.forEach((preview) => preview.url && URL.revokeObjectURL(preview.url));
  }, [files]);

  useEffect(
    () => () => {
      if (recorderRef.current?.state === "recording") {
        recorderRef.current.stop();
      }
      mediaStreamRef.current?.getTracks().forEach((track) => track.stop());
    },
    [],
  );

  const selectFiles = (selectedFiles: FileList | null) => {
    if (!selectedFiles) {
      return;
    }

    const validFiles = Array.from(selectedFiles).filter(
      (file) => file.type === "application/pdf" || file.type.startsWith("image/"),
    );
    if (validFiles.length > 10) {
      onError("A maximum of 10 attachments is allowed.");
      return;
    }

    const oversizedFile = validFiles.find((file) => file.size > 20 * 1024 * 1024);
    if (oversizedFile) {
      onError(`'${oversizedFile.name}' must be smaller than 20 MB.`);
      return;
    }

    onFilesChange(validFiles);
  };

  const toggleRecording = async () => {
    if (isRecording) {
      setIsTranscribing(true);
      recorderRef.current?.stop();
      return;
    }

    try {
      const mediaStream = await navigator.mediaDevices.getUserMedia({ audio: true });
      const recorder = new MediaRecorder(mediaStream);
      mediaStreamRef.current = mediaStream;
      recorderRef.current = recorder;
      audioChunksRef.current = [];
      recorder.addEventListener("dataavailable", (event) => {
        if (event.data.size > 0) {
          audioChunksRef.current.push(event.data);
        }
      });
      recorder.addEventListener("stop", async () => {
        try {
          const contentType = recorder.mimeType || "audio/webm";
          const audio = new Blob(audioChunksRef.current, { type: contentType });
          const transcription = await onTranscribe(audio, "recording.webm");
          onTextChange(text.trim().length === 0 ? transcription : `${text.trimEnd()} ${transcription}`);
        } catch (error) {
          onError(error instanceof Error ? error.message : "Audio transcription failed.");
        } finally {
          mediaStream.getTracks().forEach((track) => track.stop());
          mediaStreamRef.current = null;
          recorderRef.current = null;
          setIsRecording(false);
          setIsTranscribing(false);
        }
      });
      recorder.start();
      setIsRecording(true);
    } catch (error) {
      onError(error instanceof Error ? error.message : "Microphone access failed.");
    }
  };

  return (
    <form
      className="composer"
      onSubmit={(event) => {
        event.preventDefault();
        onSend();
      }}
    >
      {allowAttachments && previews.length > 0 && (
        <div className="pending-attachments">
          {previews.map((preview, index) => (
            <div className="pending-attachment" title={preview.file.name} key={`${preview.file.name}-${index}`}>
              {preview.url ? <img src={preview.url} alt={preview.file.name} /> : <span>PDF</span>}
              <Button
                appearance="primary"
                shape="circular"
                size="small"
                type="button"
                icon={<Dismiss12Regular />}
                aria-label={`Remove ${preview.file.name}`}
                onClick={() => onFilesChange(files.filter((_, fileIndex) => fileIndex !== index))}
              />
            </div>
          ))}
        </div>
      )}
      <div className="composer-controls">
        {allowAttachments && (
          <>
            <input
              ref={fileInputRef}
              className="visually-hidden"
              type="file"
              accept="application/pdf,image/*"
              multiple
              onChange={(event) => {
                selectFiles(event.target.files);
                event.target.value = "";
              }}
            />
            <Button
              appearance="subtle"
              size="large"
              type="button"
              icon={<Attach20Regular />}
              disabled={disabled}
              aria-label="Attach files"
              title="Attach files"
              onClick={() => fileInputRef.current?.click()}
            />
          </>
        )}
        <Textarea
          className="message-input"
          value={text}
          resize="vertical"
          size="large"
          disabled={disabled}
          aria-label="Message"
          placeholder="Message the assistant"
          onChange={(_, data) => onTextChange(data.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter" && !event.shiftKey) {
              event.preventDefault();
              if (!disabled) {
                onSend();
              }
            }
          }}
        />
        <Button
          appearance="primary"
          size="large"
          type="submit"
          icon={<Send20Filled />}
          disabled={disabled || text.trim().length === 0}
          aria-label="Send message"
          title="Send message"
        />
        {allowAudio && (
          <Button
            appearance={isRecording ? "primary" : "subtle"}
            size="large"
            type="button"
            icon={isRecording ? <Stop20Filled /> : <Mic20Regular />}
            disabled={disabled || isTranscribing}
            aria-label={isRecording ? "Stop recording" : "Record audio"}
            title={isTranscribing ? "Transcribing audio" : isRecording ? "Stop recording" : "Record audio"}
            onClick={toggleRecording}
          />
        )}
      </div>
    </form>
  );
}
