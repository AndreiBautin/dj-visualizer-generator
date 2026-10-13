import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { FormProvider, useForm, useWatch } from 'react-hook-form'
import {
  ApiError,
  createJob,
  createSampleJob,
  isSampleMixEnabled,
} from './api/client'
import { Backdrop } from './components/Backdrop'
import { CopyJobLink } from './components/CopyJobLink'
import { BuildFooter } from './components/BuildFooter'
import { HowItWorks } from './components/HowItWorks'
import { ProgressPanel } from './components/ProgressPanel'
import { Spinner } from './components/Spinner'
import {
  CAPTION_FONT_OPTIONS,
  RenderSettings,
} from './components/RenderSettings'
import { UploadCard } from './components/UploadCard'
import { VinylRecord } from './components/VinylRecord'
import { useJobStatus } from './hooks/useJobStatus'
import { usePageFileDrop } from './hooks/usePageFileDrop'
import { jobIdFromSearch, withJobId } from './lib/jobUrl'
import { useUploadLimits } from './hooks/useUploadLimits'
import {
  formatBytes,
  formatDuration,
  validateArtworkFile,
  validateAudioFile,
} from './lib/fileValidation'
import { BUTTON_PRIMARY, BUTTON_SECONDARY } from './lib/buttons'
import {
  DEFAULT_ROTATION_SPEED_SECONDS,
  renderSettingsSchema,
  type RenderSettingsInput,
  type RenderSettingsValues,
} from './schemas/renderSettingsSchema'

/** Isolates the one field VinylRecord needs, so dragging the rotation-speed slider re-renders
 * just the record rather than the whole two-column layout. */
function LiveVinylRecord({
  artworkFile,
  motion,
}: {
  artworkFile: File | null
  motion?: 'idle' | 'rendering' | 'stopped'
}) {
  const rotationSpeedSeconds = useWatch<RenderSettingsInput>({
    name: 'rotationSpeedSeconds',
  })
  const title = useWatch<RenderSettingsInput>({ name: 'title' })
  const captionFont = useWatch<RenderSettingsInput>({ name: 'captionFont' })
  const caption = typeof title === 'string' ? title.trim() : ''
  const fontFamily = CAPTION_FONT_OPTIONS.find(
    (option) => option.value === captionFont,
  )?.previewFontFamily
  return (
    <div className="flex flex-col items-center gap-4">
      <VinylRecord
        artworkFile={artworkFile}
        rotationSpeedSeconds={
          typeof rotationSpeedSeconds === 'number'
            ? rotationSpeedSeconds
            : DEFAULT_ROTATION_SPEED_SECONDS
        }
        motion={motion}
      />
      {/* Where and how the render's drawtext stage burns the title: centred under the
          record, white with a soft shadow, in the chosen caption face. */}
      <p
        aria-hidden="true"
        className={`max-w-full truncate text-center text-xl text-white transition-opacity duration-300 [text-shadow:2px_2px_0_rgb(0_0_0/0.5)] ${caption ? 'opacity-100' : 'opacity-0'}`}
        style={{ fontFamily }}
      >
        {caption || ' '}
      </p>
    </div>
  )
}

/** Shares the progress panel's polling query (same key), so this adds no requests. */
function JobVinylRecord({
  jobId,
  artworkFile,
}: {
  jobId: string
  artworkFile: File | null
}) {
  const { data } = useJobStatus(jobId)
  const motion =
    data?.status === 'Processing'
      ? 'rendering'
      : data?.status === 'Completed' || data?.status === 'Failed'
        ? 'stopped'
        : 'idle'
  return <LiveVinylRecord artworkFile={artworkFile} motion={motion} />
}

function App() {
  const [audioFile, setAudioFile] = useState<File | null>(null)
  const [artworkFile, setArtworkFile] = useState<File | null>(null)
  const [audioError, setAudioError] = useState<string | null>(null)
  const [artworkError, setArtworkError] = useState<string | null>(null)
  // The job lives in the address too, so a reload returns to the render and the link can be
  // shared. Possession of that link is the only access control - see AGENTS.md.
  const [jobId, setJobIdState] = useState<string | null>(() =>
    jobIdFromSearch(window.location.search),
  )
  function setJobId(next: string | null) {
    setJobIdState(next)
    window.history.replaceState(null, '', withJobId(window.location.href, next))
  }
  const limits = useUploadLimits()

  // Form and progress are two different screens sharing one URL, so the browser keeps the
  // old scroll offset across the swap - on a phone that opened the next screen part-way down.
  useEffect(() => {
    window.scrollTo({ top: 0 })
  }, [jobId])

  const methods = useForm<RenderSettingsInput, unknown, RenderSettingsValues>({
    resolver: zodResolver(renderSettingsSchema),
    defaultValues: {
      title: '',
      preset: '1080p',
      rotationSpeedSeconds: DEFAULT_ROTATION_SPEED_SECONDS,
      captionFont: 'sans-bold',
    },
  })

  const [uploadFraction, setUploadFraction] = useState(0)
  const createJobMutation = useMutation({
    mutationFn: (request: Parameters<typeof createJob>[0]) =>
      createJob(request, setUploadFraction),
  })
  const sampleJobMutation = useMutation({ mutationFn: createSampleJob })

  function handleAudioSelected(file: File) {
    setAudioFile(file)
    setAudioError(validateAudioFile(file, limits.maxAudioBytes))
  }

  function handleArtworkSelected(file: File) {
    setArtworkFile(file)
    setArtworkError(validateArtworkFile(file, limits.maxImageBytes))
  }

  // Sorted by what the file is, not where it landed: a mix goes to audio, anything else to
  // artwork, where the usual validation explains a wrong type.
  const isPageDragging = usePageFileDrop((file) => {
    if (
      file.type.startsWith('audio/') ||
      /\.(mp3|wav|flac|m4a)$/i.test(file.name)
    ) {
      handleAudioSelected(file)
    } else {
      handleArtworkSelected(file)
    }
  }, jobId === null)

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

    setUploadFraction(0)
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
        <main className="relative min-h-screen overflow-clip bg-canvas text-ink">
          <Backdrop />
          <div className="relative mx-auto flex min-h-screen w-full max-w-4xl flex-col items-center justify-center gap-8 p-8 sm:flex-row sm:gap-12">
            <div className="w-full max-w-[16rem] shrink-0 sm:max-w-[18rem]">
              <JobVinylRecord jobId={jobId} artworkFile={artworkFile} />
            </div>
            <div className="flex w-full max-w-sm flex-col items-center gap-6 text-center sm:items-start sm:text-left">
              <h1 className="bg-gradient-to-b from-white to-white/60 bg-clip-text text-3xl font-semibold tracking-[-0.03em] text-transparent">
                DJ Visualizer Generator
              </h1>
              <ProgressPanel
                jobId={jobId}
                onReset={handleReset}
                posterFile={artworkFile}
              />
              <CopyJobLink />
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
      <main className="relative min-h-screen overflow-clip bg-canvas text-ink">
        <Backdrop />
        <div
          aria-hidden="true"
          className={`pointer-events-none fixed inset-3 z-20 flex items-end justify-center rounded-3xl border-2 border-dashed border-accent/60 bg-accent/[0.06] pb-10 transition-opacity duration-200 ${isPageDragging ? 'opacity-100' : 'opacity-0'}`}
        >
          <span className="rounded-full bg-canvas/80 px-4 py-2 text-sm font-medium text-ink ring-1 ring-accent/40 backdrop-blur">
            Drop a mix or artwork anywhere
          </span>
        </div>
        <div className="relative mx-auto flex min-h-screen w-full max-w-5xl flex-col gap-10 p-6 sm:p-10 lg:flex-row lg:items-center lg:gap-16">
          <div className="flex flex-col items-center gap-6 text-center motion-safe:animate-[rise-in_600ms_cubic-bezier(.2,.8,.2,1)_both] lg:sticky lg:top-10 lg:w-2/5 lg:items-start lg:text-left">
            <div className="w-full max-w-[20rem]">
              <LiveVinylRecord artworkFile={artworkFile} />
            </div>
            <div>
              <p className="text-xs font-medium tracking-[0.2em] text-accent-glow uppercase">
                Mix to video
              </p>
              <h1 className="mt-3 bg-gradient-to-b from-white to-white/60 bg-clip-text text-5xl font-semibold tracking-[-0.035em] text-balance text-transparent">
                DJ Visualizer Generator
              </h1>
              <p className="mt-3 text-lg text-ink-muted">
                Upload a mix, get a spinning-record video.
              </p>
            </div>
          </div>

          <form
            onSubmit={methods.handleSubmit(onSubmit)}
            className="w-full space-y-4 motion-safe:animate-[rise-in_600ms_cubic-bezier(.2,.8,.2,1)_120ms_both] lg:w-3/5"
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
              <p role="alert" className="text-sm text-danger">
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
              className={`relative w-full overflow-hidden ${BUTTON_PRIMARY}`}
            >
              {createJobMutation.isPending ? (
                <>
                  {/* Fills the button left to right as the file goes up. */}
                  <span
                    aria-hidden="true"
                    className="absolute inset-y-0 left-0 bg-white/20 transition-[width] duration-300"
                    style={{ width: `${Math.round(uploadFraction * 100)}%` }}
                  />
                  <Spinner />
                  <span className="relative tabular-nums">
                    Uploading...
                    {uploadFraction > 0 &&
                      ` ${Math.round(uploadFraction * 100)}%`}
                  </span>
                </>
              ) : (
                'Generate Video'
              )}
            </button>

            {isSampleMixEnabled() && (
              <div className="mt-2 space-y-2 border-t border-line pt-5 text-center">
                <p className="text-xs text-ink-faint">No mix to hand?</p>
                <button
                  type="button"
                  onClick={handleTrySample}
                  disabled={
                    sampleJobMutation.isPending || createJobMutation.isPending
                  }
                  className={`w-full ${BUTTON_SECONDARY}`}
                >
                  {sampleJobMutation.isPending ? (
                    <>
                      <Spinner />
                      Starting...
                    </>
                  ) : (
                    'Render a sample mix'
                  )}
                </button>
                <p className="text-xs text-ink-faint">
                  Renders a short synthesized track bundled with the app -
                  nothing to upload.
                </p>
              </div>
            )}
          </form>
        </div>

        <HowItWorks />

        <div className="relative flex justify-center pb-6 lg:pb-8">
          <BuildFooter />
        </div>
      </main>
    </FormProvider>
  )
}

export default App
