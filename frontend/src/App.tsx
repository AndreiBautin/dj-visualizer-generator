import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { FormProvider, useForm } from 'react-hook-form'
import { ApiError, createJob, createSampleJob, isSampleMixEnabled } from './api/client'
import { BuildFooter } from './components/BuildFooter'
import { ProgressPanel } from './components/ProgressPanel'
import { RenderSettings } from './components/RenderSettings'
import { UploadCard } from './components/UploadCard'
import { useUploadLimits } from './hooks/useUploadLimits'
import { formatBytes, formatDuration, validateArtworkFile, validateAudioFile } from './lib/fileValidation'
import {
  renderSettingsSchema,
  type RenderSettingsInput,
  type RenderSettingsValues,
} from './schemas/renderSettingsSchema'

function App() {
  const [audioFile, setAudioFile] = useState<File | null>(null)
  const [artworkFile, setArtworkFile] = useState<File | null>(null)
  const [audioError, setAudioError] = useState<string | null>(null)
  const [artworkError, setArtworkError] = useState<string | null>(null)
  const [jobId, setJobId] = useState<string | null>(null)
  const limits = useUploadLimits()

  const methods = useForm<RenderSettingsInput, unknown, RenderSettingsValues>({
    resolver: zodResolver(renderSettingsSchema),
    defaultValues: { title: '', preset: '1080p', rotationSpeedSeconds: 3, captionFont: 'sans-bold' },
  })

  const createJobMutation = useMutation({ mutationFn: createJob })
  const sampleJobMutation = useMutation({ mutationFn: createSampleJob })

  function handleAudioSelected(file: File) {
    setAudioFile(file)
    setAudioError(validateAudioFile(file, limits.maxAudioBytes))
  }

  function handleArtworkSelected(file: File) {
    setArtworkFile(file)
    setArtworkError(validateArtworkFile(file, limits.maxImageBytes))
  }

  function handleReset() {
    setJobId(null)
    setAudioFile(null)
    setArtworkFile(null)
    setAudioError(null)
    setArtworkError(null)
    methods.reset()
    createJobMutation.reset()
    sampleJobMutation.reset()
  }

  /**
   * Renders the server's bundled sample instead of an upload, using whatever settings are
   * currently in the form. Deliberately not wired through the form's submit handler: the form
   * requires two files, and the whole point of this path is not needing them.
   */
  async function handleTrySample() {
    const values = methods.getValues()
    try {
      const result = await sampleJobMutation.mutateAsync({
        title: values.title?.trim() ? values.title.trim() : undefined,
        preset: values.preset,
        rotationSpeedSeconds: values.rotationSpeedSeconds,
        captionFont: values.captionFont,
      })
      setJobId(result.jobId)
    } catch {
      // Swallowed deliberately: this is a bare click handler, not a form submit, so an
      // unhandled rejection would escape to the window. The mutation already holds the error,
      // and it is rendered by the alert below.
    }
  }

  async function onSubmit(values: RenderSettingsValues) {
    if (!audioFile || !artworkFile || audioError || artworkError) {
      return
    }

    const result = await createJobMutation.mutateAsync({
      title: values.title,
      preset: values.preset,
      rotationSpeedSeconds: values.rotationSpeedSeconds,
      captionFont: values.captionFont,
      audioFile,
      artworkFile,
    })
    setJobId(result.jobId)
  }

  if (jobId) {
    return (
      <main className="min-h-screen bg-black text-white flex flex-col items-center justify-center gap-6 p-8">
        <h1 className="text-2xl font-semibold">DJ Visualizer Generator</h1>
        <ProgressPanel jobId={jobId} onReset={handleReset} />
        <BuildFooter />
      </main>
    )
  }

  const canSubmit = Boolean(audioFile && artworkFile && !audioError && !artworkError) && !createJobMutation.isPending

  return (
    <main className="min-h-screen bg-black text-white flex flex-col items-center justify-center gap-6 p-8">
      <div className="text-center">
        <h1 className="text-2xl font-semibold">DJ Visualizer Generator</h1>
        <p className="text-white/60">Upload a mix, get a spinning-record video.</p>
      </div>

      <FormProvider {...methods}>
        <form onSubmit={methods.handleSubmit(onSubmit)} className="w-full max-w-md space-y-4">
          <UploadCard
            label="Audio file"
            hint={`MP3, WAV, FLAC, or M4A (up to ${formatBytes(limits.maxAudioBytes)}, up to ${formatDuration(limits.maxDurationSeconds)})`}
            accept=".mp3,.wav,.flac,.m4a"
            file={audioFile}
            error={audioError}
            onFileSelected={handleAudioSelected}
            onClear={() => {
              setAudioFile(null)
              setAudioError(null)
            }}
          />
          <UploadCard
            label="Artwork"
            hint={`JPG or PNG (up to ${formatBytes(limits.maxImageBytes)})`}
            accept=".jpg,.jpeg,.png"
            file={artworkFile}
            error={artworkError}
            onFileSelected={handleArtworkSelected}
            onClear={() => {
              setArtworkFile(null)
              setArtworkError(null)
            }}
          />

          <RenderSettings />

          {(createJobMutation.isError || sampleJobMutation.isError) && (
            <p role="alert" className="text-sm text-red-400">
              {(() => {
                const error = createJobMutation.error ?? sampleJobMutation.error
                return error instanceof ApiError ? error.message : 'Something went wrong. Please try again.'
              })()}
            </p>
          )}

          <button
            type="submit"
            disabled={!canSubmit}
            className="w-full rounded-lg bg-white px-6 py-3 text-sm font-semibold text-black hover:bg-white/90 disabled:cursor-not-allowed disabled:opacity-40"
          >
            {createJobMutation.isPending ? 'Uploading...' : 'Generate Video'}
          </button>

          {isSampleMixEnabled() && (
            <div className="space-y-2 border-t border-white/10 pt-4 text-center">
              <p className="text-xs text-white/50">No mix to hand?</p>
              <button
                type="button"
                onClick={handleTrySample}
                disabled={sampleJobMutation.isPending || createJobMutation.isPending}
                className="w-full rounded-lg border border-white/25 px-6 py-3 text-sm font-semibold text-white hover:bg-white/10 disabled:cursor-not-allowed disabled:opacity-40"
              >
                {sampleJobMutation.isPending ? 'Starting...' : 'Render a sample mix'}
              </button>
              <p className="text-xs text-white/40">
                Renders a short synthesized track bundled with the app - nothing to upload.
              </p>
            </div>
          )}
        </form>
      </FormProvider>

      <BuildFooter />
    </main>
  )
}

export default App
