import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { FormProvider, useForm } from 'react-hook-form'
import { ApiError, createJob } from './api/client'
import { ProgressPanel } from './components/ProgressPanel'
import { RenderSettings } from './components/RenderSettings'
import { UploadCard } from './components/UploadCard'
import { validateArtworkFile, validateAudioFile } from './lib/fileValidation'
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

  const methods = useForm<RenderSettingsInput, unknown, RenderSettingsValues>({
    resolver: zodResolver(renderSettingsSchema),
    defaultValues: { title: '', preset: '1080p', rotationSpeedSeconds: 3, captionFont: 'sans-bold' },
  })

  const createJobMutation = useMutation({ mutationFn: createJob })

  function handleAudioSelected(file: File) {
    setAudioFile(file)
    setAudioError(validateAudioFile(file))
  }

  function handleArtworkSelected(file: File) {
    setArtworkFile(file)
    setArtworkError(validateArtworkFile(file))
  }

  function handleReset() {
    setJobId(null)
    setAudioFile(null)
    setArtworkFile(null)
    setAudioError(null)
    setArtworkError(null)
    methods.reset()
    createJobMutation.reset()
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
            hint="MP3, WAV, FLAC, or M4A (up to 2 GB, up to 6 hours)"
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
            hint="JPG or PNG (up to 25 MB)"
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

          {createJobMutation.isError && (
            <p role="alert" className="text-sm text-red-400">
              {createJobMutation.error instanceof ApiError
                ? createJobMutation.error.message
                : 'Something went wrong. Please try again.'}
            </p>
          )}

          <button
            type="submit"
            disabled={!canSubmit}
            className="w-full rounded-lg bg-white px-6 py-3 text-sm font-semibold text-black hover:bg-white/90 disabled:cursor-not-allowed disabled:opacity-40"
          >
            {createJobMutation.isPending ? 'Uploading...' : 'Generate Video'}
          </button>
        </form>
      </FormProvider>
    </main>
  )
}

export default App
