<script lang="ts">
	import type { NameSuggestionsDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { NameSource, type NameSuggestion } from '$lib/hub/generated/Yass.Core.Naming';
	import { Icon } from '$lib/icons';

	let {
		state,
		result,
		error = null,
		current = null,
		pages = 1,
		resolved = (suggestion) => suggestion.Name,
		onpick,
		onretry,
		onclose
	}: {
		state: 'reading' | 'thinking' | 'downloading' | 'ready';
		result: NameSuggestionsDto | null;
		/** Why there is less here than there should be. Shown with whatever did arrive. */
		error?: string | null;
		/**
		 * The name on the document now, so the row that matches it is marked. A picked row is
		 * styled and dropped into the template before it is saved, so this is compared against
		 * what each row would become rather than against the text it shows.
		 */
		current?: string | null;
		/** What a row would be saved as, for that comparison. Identity by default. */
		resolved?: (suggestion: NameSuggestion) => string;
		/** How many pages are being read, which the server decides and the copy repeats. */
		pages?: number;
		/** The whole row, because what a picked name becomes depends on where it came from. */
		onpick: (suggestion: NameSuggestion) => void;
		onretry: () => void;
		onclose: () => void;
	} = $props();

	const loading: Record<string, string> = $derived({
		reading: `Reading text on ${pages} page${pages === 1 ? '' : 's'}…`,
		downloading: 'Downloading the model…',
		thinking: 'Finding a good name…'
	});

	const badges: Record<NameSource, { label: string; chip: string }> = {
		[NameSource.Ocr]: { label: 'OCR', chip: 'bg-success-bg2 text-success-ink' },
		[NameSource.Ai]: { label: 'AI', chip: 'bg-ai-bg text-ai-ink' },
		[NameSource.Template]: { label: 'Template', chip: 'bg-ctl-track text-ink-3' }
	};

	function meter(confidence: number): string {
		if (confidence > 0.85) return 'bg-success';
		if (confidence > 0.7) return 'bg-accent-light';
		return 'bg-warning';
	}
</script>

<button
	type="button"
	class="fixed inset-0 z-34 cursor-default"
	aria-label="Close name suggestions"
	onclick={onclose}
></button>

<div
	class="absolute top-[calc(100%+8px)] right-0 left-0 z-35 flex animate-pop-in flex-col gap-2 rounded-[20px] bg-surface p-2 shadow-popover"
	role="dialog"
	aria-label="Name suggestions"
	data-testid="name-suggestions"
>
	<div class="flex items-center gap-2 px-1.5 pt-1">
		<span class="text-ai"><Icon name="sparkle" /></span>
		<span class="flex-1 text-[13px] font-extrabold">Name suggestions</span>
		{#if state === 'ready'}
			<button
				type="button"
				class="flex h-[30px] w-[30px] items-center justify-center rounded-full text-ink-muted transition-colors hover:bg-ctl hover:text-accent"
				onclick={onretry}
				aria-label="Read again"
				data-testid="suggest-retry"
			>
				<Icon name="rescan" size={15} />
			</button>
		{/if}
	</div>

	<!-- Above the rows, not instead of them: whatever did work is still worth offering. -->
	{#if error}
		<p
			class="rounded-[12px] bg-danger-bg px-3 py-2 text-xs font-bold text-danger"
			role="alert"
			data-testid="suggest-error"
		>
			{error}
		</p>
	{/if}

	{#if state !== 'ready'}
		<p class="px-1.5 text-xs text-ink-muted">{loading[state]}</p>
		{#each [0, 1, 2] as row (row)}
			<div
				class="h-[52px] animate-shimmer rounded-[14px]"
				style="background: linear-gradient(90deg, var(--color-ctl) 0%, var(--color-skeleton) 40%, var(--color-ctl) 80%); background-size: 200% 100%; animation-delay: {row *
					120}ms"
			></div>
		{/each}
	{:else if !result || result.Suggestions.length === 0}
		<p class="px-1.5 pb-2 text-xs text-ink-muted">
			{result?.CanRead
				? 'Nothing on these pages reads like a title.'
				: 'This server does not read page text. Set up a model in Settings, or turn on naming.ocr in yass.yaml.'}
		</p>
	{:else}
		{#each result.Suggestions as suggestion, i (suggestion.Name + i)}
			<button
				type="button"
				class="flex animate-page-in flex-col gap-1 rounded-[14px] px-3 py-2.5 text-left transition-colors hover:bg-accent-hover50 {resolved(
					suggestion
				) === current
					? 'bg-accent-50'
					: ''}"
				style="animation-delay: {i * 50}ms"
				onclick={() => onpick(suggestion)}
			>
				<span class="flex items-center gap-2">
					<span class="flex-1 truncate text-sm font-bold">{suggestion.Name}</span>
					<span
						class="flex h-5 flex-none items-center rounded-full px-1.5 text-[10px] font-extrabold tracking-[0.03em] {badges[
							suggestion.Source
						].chip}"
					>
						{badges[suggestion.Source].label}
					</span>
				</span>
				<span class="flex items-center gap-2">
					<span class="flex-1 truncate text-[11px] text-ink-muted italic">{suggestion.Reason}</span>
					<span class="h-1 w-10 flex-none overflow-hidden rounded-full bg-skeleton">
						<span
							class="block h-full rounded-full {meter(suggestion.Confidence)}"
							style="width: {Math.round(suggestion.Confidence * 100)}%"
						></span>
					</span>
				</span>
			</button>
		{/each}

		<!-- Exactly what left, not roughly: it is the one line here nobody can check. -->
		<p class="px-1.5 pb-1 text-[11px] text-ink-faint" data-testid="suggest-privacy">
			{#if result.Local}
				Read on this server. Nothing left it.
			{:else}
				The text read from the page was sent to an external model to summarise.
			{/if}
		</p>
	{/if}
</div>
