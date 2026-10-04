<script lang="ts">
	import type { ScannerStatusDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { ScannerState } from '$lib/hub/generated/Yass.Core.Scanning';
	import type { ConnectionState } from '$lib/hub/client';
	import { formatTime, scannerStateLabel } from '$lib/format';

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

	const offline = $derived(connection !== 'connected');
	const scanning = $derived(!offline && status?.State === ScannerState.Busy);

	const connectionLabel = $derived(
		connection === 'connected'
			? 'Connected'
			: connection === 'connecting'
				? 'Connecting…'
				: connection === 'reconnecting'
					? 'Reconnecting…'
					: 'Offline'
	);

	/**
	 * What the panel lists. Rows the server has nothing for are left out rather than
	 * shown with a dash: a missing device name is already said by the state.
	 */
	const rows = $derived(
		[
			['Server', connectionLabel],
			['Scanner', name ?? status?.DeviceName ?? null],
			['State', offline ? null : scannerStateLabel(status?.State)],
			['Pages so far', scanning && pages !== null ? String(pages) : null],
			['Details', status?.Message ?? null],
			['Last checked', status?.CheckedAt ? formatTime(status.CheckedAt) : null]
		].filter((row): row is [string, string] => row[1] !== null && row[1] !== '')
	);
</script>

{#if pinned}
	<!-- Click-catcher so the panel closes on any outside press, including a tap. -->
	<button
		type="button"
		class="fixed inset-0 z-30 cursor-default"
		aria-label="Close scanner details"
		onclick={onclose}
	></button>
{/if}
<div
	class="absolute top-[calc(100%+8px)] left-1/2 z-31 w-max max-w-[min(320px,calc(100vw-32px))] -translate-x-1/2 animate-pop-in rounded-[16px] bg-surface p-4 text-left shadow-menu"
	role="dialog"
	aria-label="Scanner details"
	data-testid="scanner-details"
>
	<p class="mb-2 text-[11px] font-bold tracking-[0.06em] text-ink-faint uppercase">Scanner</p>
	<dl class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-[13px]">
		{#each rows as [key, value] (key)}
			<dt class="font-semibold text-ink-muted">{key}</dt>
			<dd class="min-w-0 font-medium break-words text-ink">{value}</dd>
		{/each}
	</dl>
</div>
