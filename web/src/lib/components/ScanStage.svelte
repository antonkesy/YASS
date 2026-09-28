<script lang="ts">
	import type { ScanOptionsDto, ScannerStatusDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { ScannerState } from '$lib/hub/generated/Yass.Core.Scanning';
	import type { ConnectionState } from '$lib/hub/client';
	import { pageSizeLabel, usesFeeder } from '$lib/format';
	import { activePreset, presets } from '$lib/presets';
	import { settings } from '$lib/settings';
	import PaperStack from './PaperStack.svelte';
	import PresetPills from './PresetPills.svelte';
	import ScanButton from './ScanButton.svelte';
	import SettingsChips from './SettingsChips.svelte';
	import { Icon } from '$lib/icons';

	let {
		status,
		connection,
		options,
		busy = false,
		onscan,
		onsettings
	}: {
		status: ScannerStatusDto | null;
		connection: ConnectionState;
		options: ScanOptionsDto | null;
		busy?: boolean;
		onscan: () => void;
		onsettings: () => void;
	} = $props();

	const chosen = $derived(activePreset($presets, $settings));

	const offline = $derived(connection !== 'connected');
	const ready = $derived(!offline && status?.State === ScannerState.Ready);

	const headline = $derived(
		offline
			? connection === 'connecting'
				? 'Connecting…'
				: 'Not connected'
			: status?.State === ScannerState.Ready
				? 'Ready to scan'
				: status?.State === ScannerState.Busy
					? 'Scanner busy'
					: status?.State === ScannerState.NoDevice
						? 'No scanner found'
						: status?.State === ScannerState.Error
							? 'Scanner problem'
							: 'Checking the scanner…'
	);

	// "Feeder loaded" in the mockup, but readiness is all the scanner actually tells us,
	// so the pill names the source the scan will use rather than claiming paper is in it.
	const readyPill = $derived(usesFeeder(options?.Source) ? 'Feeder ready' : 'Flatbed ready');

	const caption = $derived(
		[pageSizeLabel(options?.PageSize), options?.Dpi ? `${options.Dpi} dpi` : '']
			.filter(Boolean)
			.join(' · ')
	);
</script>

<section
	class="flex animate-fade-in flex-col items-center gap-[18px] rounded-[28px] px-5 py-8 shadow-stage sm:px-8 sm:py-12"
	style="background: radial-gradient(120% 80% at 50% 0%, var(--color-accent-wash2) 0%, var(--color-surface) 60%)"
	style:flex="2 1 520px"
	data-testid="scan-stage"
>
	<PaperStack {caption} sweeping={busy} />

	{#if ready}
		<p
			class="flex items-center gap-[7px] rounded-full bg-success-bg px-3 py-1.5 text-[13px] font-bold text-success-ink"
		>
			<span class="h-[7px] w-[7px] rounded-full bg-success" aria-hidden="true"></span>
			{readyPill}
		</p>
	{:else if status?.Message}
		<p class="max-w-[420px] text-center text-[13px] font-semibold text-ink-muted">
			{status.Message}
		</p>
	{/if}

	<h1
		class="text-center text-[34px] font-extrabold tracking-[-0.03em] text-balance sm:text-[clamp(46px,3.6vw,60px)]"
	>
		{headline}
	</h1>

	<PresetPills
		presets={$presets}
		active={chosen?.id ?? null}
		modified={chosen === null}
		onpick={(id) => {
			const preset = $presets.find((p) => p.id === id);
			if (preset) settings.set({ ...$settings, ...preset.settings });
		}}
		onsave={onsettings}
	/>

	<SettingsChips {options} onclick={onsettings} />

	<button
		type="button"
		onclick={onsettings}
		class="flex h-9 items-center gap-2 rounded-full px-3 text-sm font-bold text-accent transition-colors hover:bg-accent-wash"
	>
		<Icon name="sliders" />
		Change settings
	</button>

	<ScanButton disabled={!ready || busy} {busy} onclick={onscan} />
</section>
