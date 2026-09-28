<script lang="ts">
	import type { ScannerStatusDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { ScannerState } from '$lib/hub/generated/Yass.Core.Scanning';
	import type { ConnectionState } from '$lib/hub/client';
	import { scannerStateLabel } from '$lib/format';

	let { status, connection }: { status: ScannerStatusDto | null; connection: ConnectionState } =
		$props();

	const offline = $derived(connection !== 'connected');
	const label = $derived(
		offline
			? connection === 'connecting'
				? 'Connecting…'
				: 'Offline'
			: scannerStateLabel(status?.State)
	);
	const tone = $derived(
		offline
			? 'bg-stone-200 text-stone-700'
			: status?.State === ScannerState.Ready
				? 'bg-emerald-100 text-emerald-800'
				: status?.State === ScannerState.Busy
					? 'bg-amber-100 text-amber-800'
					: status?.State === ScannerState.Unknown
						? 'bg-stone-200 text-stone-700'
						: 'bg-rose-100 text-rose-800'
	);
	const title = $derived(
		offline ? 'Not connected to the server' : (status?.Message ?? status?.DeviceName ?? '')
	);
</script>

<span
	class="inline-flex min-h-11 items-center gap-2 rounded-full px-4 text-sm font-medium {tone}"
	role="status"
	aria-live="polite"
	{title}
	data-testid="status-pill"
>
	<span
		class="h-2.5 w-2.5 rounded-full {offline || status?.State === ScannerState.Unknown
			? 'bg-stone-400'
			: status?.State === ScannerState.Ready
				? 'bg-emerald-500'
				: status?.State === ScannerState.Busy
					? 'animate-pulse bg-amber-500'
					: 'bg-rose-500'}"
		aria-hidden="true"
	></span>
	{label}
	{#if !offline && status?.DeviceName}
		<span class="hidden font-normal opacity-70 sm:inline">· {status.DeviceName}</span>
	{/if}
</span>
