<script lang="ts">
	import { t } from '$lib/i18n/index.svelte';
	import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
	import { canSave } from '$lib/review/download';
	import { Icon } from '$lib/icons';
	import { isDesktop } from '$lib/hub/bridge';
	import ProgressBar from './ProgressBar.svelte';
	import StopScanButton from './StopScanButton.svelte';

	let {
		session,
		progress = null,
		busy = false,
		picker = false,
		serverFolder = null,
		stopping = false,
		ondownload,
		onserver,
		onstop
	}: {
		session: SessionDto;
		progress?: number | null;
		busy?: boolean;
		picker?: boolean;
		ondownload: () => void;
		serverFolder?: string | null;
		onserver?: () => void;
		onstop?: () => void;
		stopping?: boolean;
	} = $props();

	const scanning = $derived(session.State === SessionState.Scanning);
	const exporting = $derived(session.State === SessionState.Exporting || busy);
	const canExport = $derived(canSave(session, busy));

	const primary =
		'flex h-[50px] items-center justify-center gap-2 rounded-[14px] bg-accent hover:bg-accent-hover text-[15px] font-extrabold text-white shadow-export transition-[transform,box-shadow,background-color] duration-200 hover:shadow-export-hover active:scale-[0.98] disabled:pointer-events-none disabled:opacity-40';
</script>

<div class="flex min-w-0 flex-col gap-3.5" data-testid="bottom-bar">
	{#if scanning && onstop}
		<StopScanButton {onstop} {stopping} />
	{:else if exporting}
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
			<button
				type="button"
				class="flex h-[46px] w-full items-center justify-center gap-2 rounded-[14px] bg-accent-tint text-[14px] font-extrabold text-accent-deep transition-colors hover:bg-accent-100 disabled:pointer-events-none disabled:opacity-40"
				onclick={onserver}
				disabled={!canExport}
				title={t('review.saveInto', { folder: serverFolder })}
				data-testid="server-save-button"
			>
				<Icon name="folder" />
				{t(isDesktop() ? 'review.saveToFolder' : 'review.saveToServer')}
			</button>
		{/if}
	{/if}
</div>
