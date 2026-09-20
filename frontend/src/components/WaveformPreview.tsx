import { useEffect, useState } from 'react'

// Bounds memory use regardless of the file's real size (uploads here go up to 2 GB) - only this
// many leading bytes are ever read into memory and handed to decodeAudioData. Streamable
// containers (WAV, FLAC, most MP3) decode a valid partial waveform from a prefix like this; a
// container that keeps its metadata at the end (some M4A files) or an unsupported codec will
// fail to decode entirely, which is treated as "no preview" rather than an error - see below.
const PREVIEW_BYTES = 8 * 1024 * 1024
const BAR_COUNT = 48

interface WaveformPreviewProps {
  file: File
}

export function WaveformPreview({ file }: WaveformPreviewProps) {
  const [peaks, setPeaks] = useState<number[] | null>(null)

  useEffect(() => {
    let cancelled = false
    setPeaks(null)

    async function decode() {
      const AudioContextCtor = window.AudioContext
      if (!AudioContextCtor) {
        return
      }

      const context = new AudioContextCtor()
      try {
        const buffer = await file.slice(0, PREVIEW_BYTES).arrayBuffer()
        const audioBuffer = await context.decodeAudioData(buffer)
        if (!cancelled) {
          setPeaks(extractPeaks(audioBuffer, BAR_COUNT))
        }
      } catch {
        // Best-effort preview only - FilePreview already shows the filename and size regardless
        // of whether this succeeds, so a decode failure here has nothing more to report.
      } finally {
        void context.close()
      }
    }

    void decode()

    return () => {
      cancelled = true
    }
  }, [file])

  if (!peaks) {
    return null
  }

  const max = Math.max(...peaks, 0.01)
  const barWidth = 100 / peaks.length

  return (
    <svg viewBox="0 0 100 24" preserveAspectRatio="none" className="h-6 w-full" aria-hidden="true">
      {peaks.map((peak, index) => {
        const height = Math.max(1, (peak / max) * 24)
        return (
          <rect
            key={index}
            x={index * barWidth}
            y={(24 - height) / 2}
            width={barWidth * 0.7}
            height={height}
            className="fill-white/50"
          />
        )
      })}
    </svg>
  )
}

function extractPeaks(buffer: AudioBuffer, barCount: number): number[] {
  const channelData = buffer.getChannelData(0)
  const samplesPerBar = Math.max(1, Math.floor(channelData.length / barCount))
  const peaks: number[] = []

  for (let bar = 0; bar < barCount; bar++) {
    const start = bar * samplesPerBar
    const end = Math.min(channelData.length, start + samplesPerBar)
    let peak = 0
    for (let i = start; i < end; i++) {
      const value = Math.abs(channelData[i])
      if (value > peak) {
        peak = value
      }
    }
    peaks.push(peak)
  }

  return peaks
}
