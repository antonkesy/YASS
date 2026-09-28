<script lang="ts">
	import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
	import { activePageCount } from '$lib/format';
	import { Icon } from '$lib/icons';

	let {
		session,
		progress = null,
		allowDownload = false,
		busy = false,
		savedPath = null,
		onsave,
		ondownload,
		ondiscard
	}: {
		session: SessionDto;
		progress?: number | null;
		allowDownload?: boolean;
		busy?: boolean;
		savedPath?: string | null;
		onsave: () => void;
		ondownload: () => void;
		ondiscard: () => void;
	} = $props();

	const active = $derived(activePageCount(session));
	const scanning = $derived(session.State === SessionState.Scanning);
	const exporting = $derived(session.State === SessionState.Exporting || busy);
	const canExport = $derived(
		!scanning &&
			!exporting &&
			active > 0 &&
			(session.State === SessionState.Review || session.State === SessionState.Done)
	);
	let confirmDiscard = $state(false);

	function discard() {
		if (!confirmDiscard) {
			confirmDiscard = true;
			return;
		}
		confirmDiscard = false;
		ondiscard();
	}
</script>

<div class="flex flex-col gap-3.5" data-testid="bottom-bar">
	{#if exporting || savedPath}
		<!-- The mockup swaps the buttons for a progress card while a save is running. -->
		<div
			class="flex animate-pop-in flex-col gap-2 rounded-[16px] p-3.5 transition-colors duration-300 {savedPath
				? 'bg-success-bg'
				: 'bg-accent-hover50'}"
		>
			<div class="flex items-center gap-2.5">
				<span
					class="flex h-9 w-9 flex-none items-center justify-center rounded-full {savedPath
						? 'animate-check-in bg-success text-white shadow-check-bubble'
						: 'bg-surface text-accent'}"
				>
					<Icon name={savedPath ? 'check' : 'exportTo'} />
				</span>
				<span class="flex min-w-0 flex-col">
					<span
						class="text-sm font-extrabold {savedPath ? 'text-success-ink' : 'text-ink'}"
						data-testid={savedPath ? 'saved-message' : undefined}
					>
						{savedPath ? 'Saved' : 'Saving…'}
					</span>
					{#if savedPath}
						<span class="truncate font-mono text-[11px] text-success-ink">{savedPath}</span>
					{/if}
				</span>
				{#if exporting}
					<span class="ml-auto text-[13px] font-bold text-ink-muted tabular-nums">
						{progress ?? 0}%
					</span>
				{/if}
			</div>
			{#if exporting}
				<div
					class="h-1.5 overflow-hidden rounded-full bg-accent-100"
					role="progressbar"
					aria-valuemin="0"
					aria-valuemax="100"
					aria-valuenow={progress ?? 0}
					aria-label="Export progress"
				>
					<div
						class="h-full bg-linear-to-r from-accent-sky to-accent transition-[width] duration-100 ease-linear"
						style:width="{progress ?? 5}%"
					></div>
				</div>
			{/if}
		</div>
	{:else}
		<button
			type="button"
			class="flex h-[50px] items-center justify-center gap-2 rounded-[14px] bg-linear-135 from-accent-light to-accent-grad2 text-[15px] font-extrabold text-white shadow-export transition-[transform,box-shadow] duration-200 hover:-translate-y-px hover:shadow-export-hover disabled:pointer-events-none disabled:opacity-40"
			onclick={onsave}
			disabled={!canExport}
			data-testid="save-button"
		>
			<Icon name="exportTo" />
			Save
		</button>

		{#if allowDownload}
			<button
				type="button"
				class="flex h-12 items-center justify-center gap-2 rounded-[14px] bg-accent-50 text-[15px] font-bold text-accent-deep transition-colors hover:bg-accent-100 disabled:pointer-events-none disabled:opacity-40"
				onclick={ondownload}
				disabled={!canExport}
				data-testid="download-button"
			>
				<Icon name="download" />
				Download
			</button>
		{/if}
	{/if}

	<p class="text-center text-[13px] font-semibold text-ink-muted" data-testid="page-count">
		{active} of {session.Pages.length} page{session.Pages.length === 1 ? '' : 's'}
	</p>

	<button
		type="button"
		class="flex h-[42px] items-center gap-2.5 rounded-[12px] px-3 text-sm font-semibold transition-colors {confirmDiscard
			? 'bg-danger text-white'
			: 'text-danger hover:bg-danger-bg'} disabled:pointer-events-none disabled:opacity-40"
		onclick={discard}
		onblur={() => (confirmDiscard = false)}
		disabled={scanning || exporting}
		data-testid="discard-button"
	>
		<Icon name="trash" />
		{confirmDiscard ? 'Really discard?' : 'Discard scan'}
	</button>
</div>
