import { useEffect, useRef, useState } from 'react'
import { useObjectUrl } from '../hooks/useObjectUrl'
import { formatClock } from '../lib/renderTiming'

// Best-effort preview of the file prefix, not the whole mix. This limits encoded input;
// decoded PCM can be larger. Some containers cannot decode a partial file.
const PREVIEW_BYTES = 8 * 1024 * 1024
const BAR_COUNT = 48

interface WaveformPreviewProps {
  file: File
}

export function WaveformPreview({ file }: WaveformPreviewProps) {
  const [peaks, setPeaks] = useState<number[] | null>(null)
  // The waveform covers only the decoded prefix, so the playhead is drawn against that
  // span and playback stops at its end rather than running past what the bars show.
  const [prefixSeconds, setPrefixSeconds] = useState(0)
  const [playing, setPlaying] = useState(false)
  const [position, setPosition] = useState(0)
  const audioRef = useRef<HTMLAudioElement>(null)
  const audioUrl = useObjectUrl(file)

  useEffect(() => {
    let cancelled = false
    setPeaks(null)
    setPlaying(false)
    setPosition(0)

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
          setPrefixSeconds(audioBuffer.duration)
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
  const playedFraction =
    prefixSeconds > 0 ? Math.min(1, position / prefixSeconds) : 0

  function toggle() {
    const audio = audioRef.current
    if (!audio) return
    if (playing) {
      audio.pause()
    } else {
      if (audio.currentTime >= prefixSeconds) audio.currentTime = 0
      void audio.play().catch(() => setPlaying(false))
    }
  }

  return (
    <div className="mt-1 flex items-center gap-2">
      <button
        type="button"
        onClick={toggle}
        aria-label={playing ? 'Pause preview' : 'Play preview'}
        className="relative z-10 flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-tint/[0.08] text-ink transition-colors hover:bg-tint/[0.14] focus-visible:ring-2 focus-visible:ring-accent/60 focus-visible:outline-none"
      >
        <svg
          aria-hidden="true"
          viewBox="0 0 16 16"
          className="h-3 w-3"
          fill="currentColor"
        >
          {playing ? (
            <path d="M4 3h3v10H4zM9 3h3v10H9z" />
          ) : (
            <path d="M5 3l8 5-8 5z" />
          )}
        </svg>
      </button>
      <svg
        viewBox="0 0 100 24"
        preserveAspectRatio="none"
        className="h-6 w-full"
        aria-hidden="true"
      >
        {peaks.map((peak, index) => {
          const height = Math.max(1, (peak / max) * 24)
          const played = (index + 0.5) / peaks.length <= playedFraction
          return (
            <rect
              key={index}
              x={index * barWidth}
              y={(24 - height) / 2}
              width={barWidth * 0.7}
              height={height}
              className={played ? 'fill-accent-glow' : 'fill-tint/40'}
            />
          )
        })}
      </svg>
      {/* Only the first PREVIEW_BYTES are decoded, which is seconds of WAV but minutes of MP3 -
          so say how much the bars and the play button actually cover. */}
      {file.size > PREVIEW_BYTES && prefixSeconds > 0 && (
        <span className="shrink-0 text-[11px] text-ink-faint tabular-nums">
          first {formatClock(prefixSeconds * 1000)}
        </span>
      )}
      {audioUrl && (
        <audio
          ref={audioRef}
          src={audioUrl}
          preload="none"
          onPlay={() => setPlaying(true)}
          onPause={() => setPlaying(false)}
          onTimeUpdate={(event) => {
            const time = event.currentTarget.currentTime
            setPosition(time)
            if (time >= prefixSeconds) event.currentTarget.pause()
          }}
        />
      )}
    </div>
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
