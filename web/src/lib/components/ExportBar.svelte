<script lang="ts">
	import { t } from '$lib/i18n/index.svelte';
	import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
	import { activePageCount } from '$lib/format';
	import { Icon } from '$lib/icons';
	import ProgressBar from './ProgressBar.svelte';

	let {
		session,
		progress = null,
		busy = false,
		picker = false,
		serverFolder = null,
		ondownload,
		onserver
	}: {
		session: SessionDto;
		progress?: number | null;
		busy?: boolean;
		/** Whether the browser can offer a save dialog, which is what the button says it will do. */
		picker?: boolean;
		ondownload: () => void;
		/**
		 * The server's output folder, when saving there is configured and this browser has
		 * switched it on in Settings. Null hides the button.
		 */
		serverFolder?: string | null;
		onserver?: () => void;
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

	const primary =
		'flex h-[50px] items-center justify-center gap-2 rounded-[14px] bg-linear-135 from-accent-light to-accent-grad2 text-[15px] font-extrabold text-white shadow-export transition-[transform,box-shadow] duration-200 hover:-translate-y-px hover:shadow-export-hover disabled:pointer-events-none disabled:opacity-40';
</script>

<div class="flex min-w-0 flex-col gap-3.5" data-testid="bottom-bar">
	{#if exporting}
		<!-- The mockup swaps the button for a progress card while the document is being built. -->
		<div class="flex animate-pop-in flex-col gap-2 rounded-[16px] bg-accent-hover50 p-3.5">
			<div class="flex items-center gap-2.5">
				<span
					class="flex h-9 w-9 flex-none items-center justify-center rounded-full bg-surface text-accent"
				>
					<Icon name="exportTo" />
				</span>
				<span class="text-sm font-extrabold text-ink">{t('review.preparing')}</span>
				<span class="ml-auto text-[13px] font-bold text-ink-muted tabular-nums">
					{progress ?? 0}%
				</span>
			</div>
			<ProgressBar percent={progress} label={t('review.exportProgress')} />
		</div>
	{:else}
		<!--
			One button for "get the file off the server": it opens the browser's own save
			dialog where there is one, and is a plain download where there is not.
		-->
		<button
			type="button"
			class="w-full {primary}"
			onclick={ondownload}
			disabled={!canExport}
			data-testid="download-button"
		>
			<Icon name="download" />
			{picker ? t('review.saveAs') : t('review.download')}
		</button>
		{#if serverFolder && onserver}
			<!-- A second way out, not a replacement: the download is still one click. -->
			<button
				type="button"
				class="flex h-[46px] w-full items-center justify-center gap-2 rounded-[14px] bg-accent-tint text-[14px] font-extrabold text-accent-deep transition-colors hover:bg-accent-100 disabled:pointer-events-none disabled:opacity-40"
				onclick={onserver}
				disabled={!canExport}
				title={t('review.saveInto', { folder: serverFolder })}
				data-testid="server-save-button"
			>
				<Icon name="folder" />
				{t('review.saveToServer')}
			</button>
		{/if}
	{/if}
</div>
