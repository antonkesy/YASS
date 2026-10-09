<script lang="ts">
	import { useLayer } from '$lib/keys.svelte';
	import type { ScannerStatusDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { ScannerState } from '$lib/hub/generated/Yass.Core.Scanning';
	import type { ConnectionState } from '$lib/hub/client';
	import { formatTime, scannerStateLabel } from '$lib/format';
	import { t } from '$lib/i18n/index.svelte';

	let {
		status,
		connection,
		name = null,
		pages = null,
		pinned = true,
		onclose
	}: {
		status: ScannerStatusDto | null;
		connection: ConnectionState;
		/** The scanner the panel is about, when it is not the one the status names. */
		name?: string | null;
		/** Pages scanned so far, when a scan is running. */
		pages?: number | null;
		/** Opened by a press, so it stays until an outside press; a hover has no catcher. */
		pinned?: boolean;
		onclose: () => void;
	} = $props();

	// Escape closes this, and only this, while it is open.
	useLayer(() => onclose());

	const offline = $derived(connection !== 'connected');
	const scanning = $derived(!offline && status?.State === ScannerState.Busy);

	const connectionLabel = $derived(
		connection === 'connected'
			? t('home.connected')
			: connection === 'connecting'
				? t('home.connecting')
				: connection === 'reconnecting'
					? t('home.reconnecting')
					: t('home.offline')
	);

	/**
	 * What the panel lists. Rows the server has nothing for are left out rather than
	 * shown with a dash: a missing device name is already said by the state.
	 */
	const rows = $derived(
		[
			[t('home.detailServer'), connectionLabel],
			[t('home.scanner'), name ?? status?.DeviceName ?? null],
			[t('home.detailState'), offline ? null : scannerStateLabel(status?.State)],
			[t('home.detailPagesSoFar'), scanning && pages !== null ? String(pages) : null],
			[t('home.detailDetails'), status?.Message ?? null],
			[t('home.detailLastChecked'), status?.CheckedAt ? formatTime(status.CheckedAt) : null]
		].filter((row): row is [string, string] => row[1] !== null && row[1] !== '')
	);
</script>

{#if pinned}
	<!-- Click-catcher so the panel closes on any outside press, including a tap. -->
	<button
		type="button"
		class="fixed inset-0 z-30 cursor-default"
		aria-label={t('home.closeScannerDetails')}
		onclick={onclose}
	></button>
{/if}
<div
	class="absolute top-[calc(100%+8px)] left-1/2 z-31 w-max max-w-[min(320px,calc(100vw-32px))] -translate-x-1/2 animate-pop-in rounded-[16px] bg-surface p-4 text-left shadow-menu"
	role="dialog"
	aria-label={t('home.scannerDetails')}
	data-testid="scanner-details"
>
	<p class="mb-2 text-[12px] font-semibold text-ink-faint">
		{t('home.scanner')}
	</p>
	<dl class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-[13px]">
		{#each rows as [key, value] (key)}
			<dt class="font-semibold text-ink-muted">{key}</dt>
			<dd class="min-w-0 font-medium break-words text-ink">{value}</dd>
		{/each}
	</dl>
</div>
