<script lang="ts">
	import { resolve } from '$app/paths';
	import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
	import { activePageCount } from '$lib/format';
	import { Icon } from '$lib/icons';
	import SaveToMenu from './SaveToMenu.svelte';

	let {
		session,
		progress = null,
		allowDownload = false,
		busy = false,
		savedPath = null,
		exportDirectory = '',
		exportEnabled = true,
		roots = [],
		recent = [],
		onsave,
		ondownload
	}: {
		session: SessionDto;
		progress?: number | null;
		allowDownload?: boolean;
		busy?: boolean;
		savedPath?: string | null;
		/** Where the one-click Export writes, shown under the button. */
		exportDirectory?: string;
		/** `output.allow_export_button` equivalent: the user's own preference. */
		exportEnabled?: boolean;
		roots?: string[];
		recent?: string[];
		/** A directory of null means "wherever Export goes". */
		onsave: (directory: string | null) => void;
		ondownload: () => void;
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

	let saveTo = $state(false);

	const primary =
		'flex h-[50px] items-center justify-center gap-2 rounded-[14px] bg-linear-135 from-accent-light to-accent-grad2 text-[15px] font-extrabold text-white shadow-export transition-[transform,box-shadow] duration-200 hover:-translate-y-px hover:shadow-export-hover disabled:pointer-events-none disabled:opacity-40';
	const secondary =
		'flex h-12 items-center justify-center gap-2 rounded-[14px] bg-accent-50 text-[15px] font-bold text-accent-deep transition-colors hover:bg-accent-100 disabled:pointer-events-none disabled:opacity-40';
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
		{#if exportEnabled}
			<button
				type="button"
				class={primary}
				onclick={() => onsave(null)}
				disabled={!canExport}
				data-testid="save-button"
			>
				<Icon name="exportTo" />
				Export
			</button>
			{#if exportDirectory}
				<!-- Says where Export goes, and is the way to change it: the settings sheet. -->
				<a
					class="-mt-1.5 flex min-w-0 items-center gap-2 rounded-[10px] px-2 py-1.5 text-xs font-semibold text-ink-muted transition-colors hover:bg-accent-hover50 hover:text-accent"
					href={resolve('/settings')}
					title="Change export folder in settings"
					data-testid="export-path"
				>
					<Icon name="folder" size={14} />
					<span class="truncate font-mono">{exportDirectory}</span>
				</a>
			{/if}
		{/if}

		<div class="relative">
			<button
				type="button"
				class="w-full {exportEnabled ? secondary : primary}"
				onclick={() => (saveTo = !saveTo)}
				disabled={!canExport}
				aria-expanded={saveTo}
				data-testid="save-to-button"
			>
				<Icon name="folderOpen" />
				Save to…
			</button>

			{#if saveTo}
				<SaveToMenu
					{roots}
					{recent}
					onpick={(directory) => {
						saveTo = false;
						onsave(directory);
					}}
					onclose={() => (saveTo = false)}
				/>
			{/if}
		</div>

		{#if allowDownload}
			<button
				type="button"
				class={secondary}
				onclick={ondownload}
				disabled={!canExport}
				data-testid="download-button"
			>
				<Icon name="download" />
				Download
			</button>
		{/if}
	{/if}

	<!-- True as written: the session is on disk and listed the moment it is scanned. -->
	<p
		class="flex items-center gap-2 rounded-[12px] bg-success-bg px-3 py-2.5 text-xs font-bold text-success-ink"
		data-testid="autosave-strip"
	>
		<Icon name="checkSmall" />
		{scanning ? 'Auto-saving to past scans…' : 'Auto-saved to past scans'}
	</p>

	<p class="text-center text-[13px] font-semibold text-ink-muted" data-testid="page-count">
		{active} of {session.Pages.length} page{session.Pages.length === 1 ? '' : 's'}
	</p>
</div>
