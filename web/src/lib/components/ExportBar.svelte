<script lang="ts">
	import { resolve } from '$app/paths';
	import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
	import { activePageCount, folderName } from '$lib/format';
	import { Icon } from '$lib/icons';
	import ProgressBar from './ProgressBar.svelte';

	let {
		session,
		progress = null,
		allowDownload = false,
		busy = false,
		savedPath = null,
		exportDirectory = '',
		exportEnabled = true,
		picker = false,
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
		/** Whether the browser can offer a save dialog, which is what the button says it will do. */
		picker?: boolean;
		onsave: () => void;
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

	// Export can be switched off, but not down to nothing: with downloads disabled by
	// config it is the only way a document leaves the machine.
	const showExport = $derived(exportEnabled || !allowDownload);

	const primary =
		'flex h-[50px] items-center justify-center gap-2 rounded-[14px] bg-linear-135 from-accent-light to-accent-grad2 text-[15px] font-extrabold text-white shadow-export transition-[transform,box-shadow] duration-200 hover:-translate-y-px hover:shadow-export-hover disabled:pointer-events-none disabled:opacity-40';
	const secondary =
		'flex h-12 items-center justify-center gap-2 rounded-[14px] bg-accent-50 text-[15px] font-bold text-accent-deep transition-colors hover:bg-accent-100 disabled:pointer-events-none disabled:opacity-40';
</script>

<div class="flex min-w-0 flex-col gap-3.5" data-testid="bottom-bar">
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
				<ProgressBar percent={progress} label="Export progress" />
			{/if}
		</div>
	{:else}
		{#if showExport}
			<button
				type="button"
				class={primary}
				onclick={onsave}
				disabled={!canExport}
				data-testid="save-button"
			>
				<Icon name="exportTo" />
				Export
			</button>
			{#if exportDirectory}
				<!-- Says where Export goes, and is the way to change it: the settings sheet. -->
				<a
					class="-mt-1.5 flex w-full min-w-0 items-center gap-2 rounded-[10px] px-2 py-1.5 text-xs font-semibold text-ink-muted transition-colors hover:bg-accent-hover50 hover:text-accent"
					href={resolve('/settings')}
					title="{exportDirectory} — change it in settings"
					data-testid="export-path"
				>
					<Icon name="folder" size={14} />
					<!-- The folder's name; the whole path is on the tooltip, where it has the room. -->
					<span class="truncate"
						>to <span class="font-mono">{folderName(exportDirectory)}</span></span
					>
				</a>
			{/if}
		{/if}

		{#if allowDownload}
			<!--
				One button for "get the file off the server": it opens the browser's own save
				dialog where there is one, and is a plain download where there is not.
			-->
			<button
				type="button"
				class="w-full {showExport ? secondary : primary}"
				onclick={ondownload}
				disabled={!canExport}
				data-testid="download-button"
			>
				<Icon name="download" />
				{picker ? 'Save as…' : 'Download'}
			</button>
		{/if}
	{/if}
</div>
