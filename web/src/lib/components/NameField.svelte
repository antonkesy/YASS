<script lang="ts">
	import { Icon } from '$lib/icons';

	let {
		value,
		placeholder,
		generated = null,
		busy = false,
		percent = null,
		reason = '',
		canSuggest = true,
		extension = '.pdf',
		onrename,
		onsuggest
	}: {
		/** The stored name, or null when the scan has never been named. */
		value: string | null;
		/** Shown when unnamed — the timestamp the list falls back to. */
		placeholder: string;
		/**
		 * Where the found name sits inside {@link value}, when it is still in there. Shown as a
		 * chip so the part that came off the page is telling you so, rather than looking like
		 * something that was typed.
		 */
		generated?: {
			before: string;
			generated: string;
			after: string;
			/** False while the slot is still waiting to be filled. */
			filled: boolean;
		} | null;
		/** A name is being worked out right now. */
		busy?: boolean;
		/** How far the model's weights have got, when that is what the wait is. */
		percent?: number | null;
		/** Why a name cannot be asked for, for the tooltip. Empty means it can. */
		reason?: string;
		/** Whether asking for a name is on offer at all. */
		canSuggest?: boolean;
		/** What the saved file will end in, which is the export format's and not always PDF. */
		extension?: string;
		onrename: (name: string) => void;
		onsuggest: () => void;
	} = $props();

	// Seeded by the effect below rather than from `value` directly, which would only
	// ever capture what it was on the first render.
	let draft = $state('');
	let editing = $state(false);
	let field = $state<HTMLInputElement | null>(null);

	// While the field is not being edited it mirrors the server, so a rename from
	// another tab shows up here too.
	$effect(() => {
		if (!editing) draft = value ?? '';
	});

	/** Segments only earn their keep when there is a generated run to point at. */
	const segmented = $derived(!editing && generated !== null);

	function commit() {
		editing = false;
		if (draft.trim() !== (value ?? '')) onrename(draft.trim());
	}

	export function edit() {
		editing = true;
		requestAnimationFrame(() => {
			field?.focus();
			field?.select();
		});
	}
</script>

<div
	class="flex min-h-12 items-center gap-2 rounded-[14px] border-[1.5px] border-transparent bg-app px-3 py-1.5 transition-colors focus-within:border-accent hover:bg-accent-hover50"
	data-testid="document-name"
	data-name={value ?? ''}
>
	<span class="text-ink-faint"><Icon name="pencil" /></span>

	{#if segmented && generated}
		<!-- Reads as one name, but the middle of it is a button: that is the part a scan fills
		     in, and the part worth being able to change without retyping the rest. -->
		<span
			class="min-w-0 flex-1 py-1 text-base font-extrabold tracking-[-0.01em] break-words"
			data-testid="name-segments"
			>{#if generated.before}<button
					type="button"
					class="cursor-text text-left whitespace-pre-wrap"
					aria-label="Edit the whole name"
					onclick={edit}>{generated.before}</button
				>{/if}<button
				type="button"
				class="mx-px inline-flex max-w-full translate-y-px items-center rounded-[7px] px-1.5 align-bottom transition-colors hover:bg-accent-100 {generated.filled
					? 'bg-ai-bg text-ai-ink'
					: 'bg-ctl-track text-ink-muted italic'}"
				title={generated.filled
					? 'Read off the page — click to change it'
					: 'Click to name this from the page'}
				onclick={onsuggest}
				data-testid="name-generated"
				><span class="truncate">{generated.filled ? generated.generated : 'name it'}</span><span
					class="ml-1 shrink-0 text-[9px] opacity-70">▾</span
				></button
			>{#if generated.after}<button
					type="button"
					class="cursor-text text-left whitespace-pre-wrap"
					aria-label="Edit the whole name"
					onclick={edit}>{generated.after}</button
				>{/if}</span
		>
	{:else}
		<label class="flex min-w-0 flex-1 items-center">
			<span class="sr-only">Document name</span>
			<input
				bind:this={field}
				class="min-w-0 flex-1 bg-transparent text-base font-extrabold tracking-[-0.01em] outline-none"
				type="text"
				placeholder={busy ? 'Finding a name…' : placeholder}
				bind:value={draft}
				onfocus={() => (editing = true)}
				onblur={commit}
				onkeydown={(e) => {
					if (e.key === 'Enter') e.currentTarget.blur();
					if (e.key === 'Escape') {
						draft = value ?? '';
						editing = false;
						e.currentTarget.blur();
					}
				}}
				data-testid="name-input"
			/>
		</label>
	{/if}

	<!--
		Here whether or not anything has been generated: a template with no {NAME} in it still
		has to have a way to ask for a name, now that the Find name button has gone. Gone with
		the rest of it, though, when this browser reads no pages.
	-->
	{#if canSuggest}
		<button
			type="button"
			class="relative flex h-7 w-7 shrink-0 items-center justify-center overflow-hidden rounded-[8px] text-ink-faint transition-colors hover:bg-accent-50 hover:text-accent disabled:pointer-events-none {busy
				? 'text-ai-ink'
				: ''} {busy && percent === null ? 'animate-pulse' : ''} {percent === null
				? 'disabled:opacity-40'
				: ''}"
			title={reason ||
				(percent !== null ? `Downloading the model… ${percent}%` : 'Name this from the page')}
			aria-label="Name this from the page"
			disabled={reason !== '' || busy}
			onclick={onsuggest}
			data-testid="name-suggest"
		>
			{#if percent !== null}
				<!--
					The download happens behind this button, so it fills up: the popover carries the
					bar and the figure, and this is what is left to look at when it is scrolled past
					or closed. Not a pulse — a pulse says "wait" and says nothing about how long.
				-->
				<span
					class="absolute inset-x-0 bottom-0 bg-ai-bg transition-[height] duration-100 ease-linear"
					style:height="{percent}%"
					data-testid="name-suggest-progress"
				></span>
			{/if}
			<span class="relative"><Icon name="sparkle" /></span>
		</button>
	{/if}

	<span class="font-mono text-[13px] font-semibold text-ink-faint">{extension}</span>
</div>
