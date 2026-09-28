<script lang="ts">
	import type { ScannerStatusDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { ScannerState } from '$lib/hub/generated/Yass.Core.Scanning';
	import type { ConnectionState } from '$lib/hub/client';
	import { scannerStateLabel } from '$lib/format';

	let {
		status,
		connection,
		pages = null
	}: {
		status: ScannerStatusDto | null;
		connection: ConnectionState;
		/** Pages scanned so far, when a scan is running. Shown as "Scanning · page N". */
		pages?: number | null;
	} = $props();

	const offline = $derived(connection !== 'connected');
	const scanning = $derived(!offline && status?.State === ScannerState.Busy);

	const label = $derived(
		offline
			? connection === 'connecting'
				? 'Connecting…'
				: 'Offline'
			: scanning && pages !== null
				? `Scanning · page ${pages + 1}`
				: scannerStateLabel(status?.State)
	);

	// The mockup pills are always white; only the dot carries the state.
	const dot = $derived(
		offline || status?.State === ScannerState.Unknown
			? 'bg-ink-faint'
			: status?.State === ScannerState.Ready
				? 'bg-success animate-pulse-ready'
				: scanning
					? 'bg-accent animate-pulse-busy'
					: 'bg-danger'
	);

	const title = $derived(
		offline ? 'Not connected to the server' : (status?.Message ?? status?.DeviceName ?? '')
	);
</script>

<span
	class="inline-flex h-[34px] items-center gap-2 rounded-full bg-surface py-0 pr-[14px] pl-3 text-[13px] font-semibold text-ink shadow-chip"
	role="status"
	aria-live="polite"
	{title}
	data-testid="status-pill"
>
	<span class="h-2 w-2 flex-none rounded-full {dot}" aria-hidden="true"></span>
	{label}
	{#if !offline && !scanning && status?.DeviceName}
		<span class="hidden font-medium text-ink-muted sm:inline">· {status.DeviceName}</span>
	{/if}
</span>
