import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { FormProvider, useForm, useWatch } from 'react-hook-form'
import {
  ApiError,
  createJob,
  createSampleJob,
  isSampleMixEnabled,
} from './api/client'
import { BuildFooter } from './components/BuildFooter'
import { ProgressPanel } from './components/ProgressPanel'
import { RenderSettings } from './components/RenderSettings'
import { UploadCard } from './components/UploadCard'
import { VinylRecord } from './components/VinylRecord'
import { useUploadLimits } from './hooks/useUploadLimits'
import {
  formatBytes,
  formatDuration,
  validateArtworkFile,
  validateAudioFile,
} from './lib/fileValidation'
import {
  renderSettingsSchema,
  type RenderSettingsInput,
  type RenderSettingsValues,
} from './schemas/renderSettingsSchema'

/** Isolates the one field VinylRecord needs, so dragging the rotation-speed slider re-renders
 * just the record rather than the whole two-column layout. */
function LiveVinylRecord({ artworkFile }: { artworkFile: File | null }) {
  const rotationSpeedSeconds = useWatch<RenderSettingsInput>({
    name: 'rotationSpeedSeconds',
  })
  return (
    <VinylRecord
      artworkFile={artworkFile}
      rotationSpeedSeconds={
        typeof rotationSpeedSeconds === 'number' ? rotationSpeedSeconds : 3
      }
    />
  )
}

function App() {
  const [audioFile, setAudioFile] = useState<File | null>(null)
  const [artworkFile, setArtworkFile] = useState<File | null>(null)
  const [audioError, setAudioError] = useState<string | null>(null)
  const [artworkError, setArtworkError] = useState<string | null>(null)
  const [jobId, setJobId] = useState<string | null>(null)
  const limits = useUploadLimits()

  const methods = useForm<RenderSettingsInput, unknown, RenderSettingsValues>({
    resolver: zodResolver(renderSettingsSchema),
    defaultValues: {
      title: '',
      preset: '1080p',
      rotationSpeedSeconds: 3,
      captionFont: 'sans-bold',
    },
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

    try {
      const result = await createJobMutation.mutateAsync({
        title: values.title,
        preset: values.preset,
        rotationSpeedSeconds: values.rotationSpeedSeconds,
        captionFont: values.captionFont,
        audioFile,
        artworkFile,
      })
      setJobId(result.jobId)
    } catch {
      // Swallowed deliberately, same reasoning as handleTrySample below: the mutation already
      // holds the error and it's rendered by the alert further down, so letting this reject
      // upward would only be an unhandled-rejection console warning with nothing to observe it.
    }
  }

  if (jobId) {
    return (
      <FormProvider {...methods}>
        <main className="relative min-h-screen overflow-hidden bg-black text-white">
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_20%_20%,color-mix(in_oklch,var(--color-accent)_14%,transparent),transparent_55%)]" />
          <div className="relative mx-auto flex min-h-screen w-full max-w-4xl flex-col items-center justify-center gap-8 p-8 sm:flex-row sm:gap-12">
            <div className="w-full max-w-[16rem] shrink-0 sm:max-w-[18rem]">
              <LiveVinylRecord artworkFile={artworkFile} />
            </div>
            <div className="flex w-full max-w-sm flex-col items-center gap-6 text-center sm:items-start sm:text-left">
              <h1 className="text-3xl font-semibold tracking-tight">
                DJ Visualizer Generator
              </h1>
              <ProgressPanel jobId={jobId} onReset={handleReset} />
            </div>
          </div>
          <div className="relative flex justify-center pb-6">
            <BuildFooter />
          </div>
        </main>
      </FormProvider>
    )
  }

  const canSubmit =
    Boolean(audioFile && artworkFile && !audioError && !artworkError) &&
    !createJobMutation.isPending

  return (
    <FormProvider {...methods}>
      <main className="relative min-h-screen overflow-hidden bg-black text-white">
        <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_20%_15%,color-mix(in_oklch,var(--color-accent)_14%,transparent),transparent_55%)]" />
        <div className="relative mx-auto flex min-h-screen w-full max-w-5xl flex-col gap-10 p-6 sm:p-10 lg:flex-row lg:items-center lg:gap-16">
          <div className="flex flex-col items-center gap-6 text-center lg:sticky lg:top-10 lg:w-2/5 lg:items-start lg:text-left">
            <div className="w-full max-w-[20rem]">
              <LiveVinylRecord artworkFile={artworkFile} />
            </div>
            <div>
              <h1 className="text-4xl font-semibold tracking-tight text-balance">
                DJ Visualizer Generator
              </h1>
              <p className="mt-2 text-white/60">
                Upload a mix, get a spinning-record video.
              </p>
            </div>
          </div>

          <form
            onSubmit={methods.handleSubmit(onSubmit)}
            className="w-full space-y-4 lg:w-3/5"
          >
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
                  const error =
                    createJobMutation.error ?? sampleJobMutation.error
                  return error instanceof ApiError
                    ? error.message
                    : "That upload didn't make it — mind trying again?"
                })()}
              </p>
            )}

            <button
              type="submit"
              disabled={!canSubmit}
              className="w-full rounded-lg bg-accent px-6 py-3 text-sm font-semibold text-accent-content shadow-md shadow-black/30 transition-colors hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:brightness-100"
            >
              {createJobMutation.isPending ? 'Uploading...' : 'Generate Video'}
            </button>

            {isSampleMixEnabled() && (
              <div className="space-y-2 border-t border-white/10 pt-4 text-center">
                <p className="text-xs text-white/50">No mix to hand?</p>
                <button
                  type="button"
                  onClick={handleTrySample}
                  disabled={
                    sampleJobMutation.isPending || createJobMutation.isPending
                  }
                  className="w-full rounded-lg border border-white/25 px-6 py-3 text-sm font-semibold text-white hover:bg-white/10 disabled:cursor-not-allowed disabled:opacity-40"
                >
                  {sampleJobMutation.isPending
                    ? 'Starting...'
                    : 'Render a sample mix'}
                </button>
                <p className="text-xs text-white/40">
                  Renders a short synthesized track bundled with the app -
                  nothing to upload.
                </p>
              </div>
            )}
          </form>
        </div>

        <div className="relative flex justify-center pb-6 lg:pb-8">
          <BuildFooter />
        </div>
      </main>
    </FormProvider>
  )
}

export default App
