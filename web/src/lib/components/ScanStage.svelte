<script lang="ts">
	import { autoAnimate } from '$lib/autoAnimate';
	import { bounceWhenPicked } from '$lib/motion';
	import { t } from '$lib/i18n/index.svelte';
	import type {
		ScannerDeviceDto,
		ScanOptionsDto,
		ScannerStatusDto
	} from '$lib/hub/generated/Yass.Core.Hub';
	import { ScannerState } from '$lib/hub/generated/Yass.Core.Scanning';
	import type { ConnectionState } from '$lib/hub/client';
	import { pageSizeLabel } from '$lib/format';
	import {
		activePreset,
		applyPreset,
		exportPrefs,
		pickedPreset,
		presets,
		presetsFor,
		startingPreset
	} from '$lib/presets';
	import { exportFormatLabels } from '$lib/format';
	import { settings } from '$lib/settings';
	import PaperStack from './PaperStack.svelte';
	import PresetPills from './PresetPills.svelte';
	import ScanButton from './ScanButton.svelte';
	import SettingsChips from './SettingsChips.svelte';
	import { Icon } from '$lib/icons';
	import ScannerDetails from './ScannerDetails.svelte';
	import ScannerNotice from './ScannerNotice.svelte';
	import FindScannersButton from './FindScannersButton.svelte';

	let {
		status,
		connection,
		options,
		devices = [],
		deviceName = null,
		busy = false,
		onscan,
		onsettings,
		onsavepreset,
		onfound
	}: {
		status: ScannerStatusDto | null;
		connection: ConnectionState;
		options: ScanOptionsDto | null;
		devices?: ScannerDeviceDto[];
		deviceName?: string | null;
		busy?: boolean;
		onscan: () => void;
		onsettings: () => void;
		onsavepreset?: () => void;
		onfound?: (devices: ScannerDeviceDto[]) => void;
	} = $props();

	let details = $state<'hover' | 'pinned' | null>(null);

	const bounce = bounceWhenPicked(() => shown);

	const device = $derived(options?.Device || '');

	const canPickDevice = $derived(devices.length > 0);

	// The server's status names the scanner rather than identifying it, so match by name.
	const shown = $derived(
		device || devices.find((d) => d.Name === status?.DeviceName)?.Id || devices[0]?.Id || ''
	);

	// Keep a scanner the server stopped listing, or the row would light the wrong one.
	const listed = $derived(
		device && !devices.some((d) => d.Id === device)
			? [...devices, { Id: device, Name: t('home.deviceNotFound', { device }) }]
			: devices
	);

	let pills = $state<HTMLElement | null>(null);

	function rove(e: KeyboardEvent) {
		const step =
			e.key === 'ArrowRight' || e.key === 'ArrowDown'
				? 1
				: e.key === 'ArrowLeft' || e.key === 'ArrowUp'
					? -1
					: 0;
		if (step === 0 || listed.length === 0) return;
		e.preventDefault();
		const at = listed.findIndex((s) => s.Id === shown);
		const next = listed[(at + step + listed.length) % listed.length];
		settings.set({ ...$settings, Device: next.Id });
		pills?.querySelector<HTMLElement>(`[data-device="${CSS.escape(next.Id)}"]`)?.focus();
	}

	const offered = $derived(presetsFor($presets, device || undefined));

	// Against the effective options, so no overrides still matches the server's default preset.
	const chosen = $derived(options ? activePreset(offered, options, $pickedPreset.id) : null);
	const edited = $derived(
		chosen || !options ? null : (offered.find((p) => p.id === $pickedPreset.id) ?? null)
	);

	$effect(() => {
		if (!options) return;
		const start = startingPreset(offered, $settings, $pickedPreset.id, chosen);
		if (!start) return;
		settings.set(applyPreset(start, $settings));
		pickedPreset.set({ id: start.id });
	});

	const offline = $derived(connection !== 'connected');
	const ready = $derived(!offline && status?.State === ScannerState.Ready);
	// Not mid-scan: the server would only hand back its cached list then.
	const canLook = $derived(!offline && status?.State !== ScannerState.Busy);

	const blocked = $derived(
		offline
			? connection === 'connecting'
				? t('home.blockedConnecting')
				: t('home.blockedOffline')
			: status?.State === ScannerState.Busy
				? t('home.blockedBusy')
				: status?.State === ScannerState.NoDevice
					? t('home.blockedNoDevice')
					: status?.State === ScannerState.Error
						? t('home.blockedError')
						: t('home.blockedChecking')
	);

	const trouble = $derived<{
		tone: 'warning' | 'danger';
		title: string;
		hint: string;
		detail: string | null;
	} | null>(
		offline && connection !== 'connecting'
			? { tone: 'warning', title: blocked, hint: t('home.offlineHint'), detail: null }
			: status?.State === ScannerState.NoDevice
				? {
						tone: 'warning',
						title: blocked,
						hint: t('home.noDeviceHint'),
						detail: status.Message ?? null
					}
				: status?.State === ScannerState.Error
					? {
							tone: 'danger',
							title: blocked,
							hint: t('home.errorHint'),
							detail: status.Message ?? null
						}
					: null
	);

	const caption = $derived(
		[pageSizeLabel(options?.PageSize), options?.Dpi ? t('home.dpi', { n: options.Dpi }) : '']
			.filter(Boolean)
			.join(' · ')
	);
</script>

<section
	class="flex min-h-0 animate-fade-in flex-col items-center justify-center gap-[clamp(8px,1.4*var(--vh),18px)] overflow-y-auto rounded-[28px] bg-surface px-5 py-8 shadow-stage sm:px-8 sm:py-[clamp(1rem,2.6*var(--vh),3rem)]"
	style:flex="2 1 520px"
	data-testid="scan-stage"
	{@attach autoAnimate}
>
	<PaperStack {caption} pulse={chosen?.id ?? 'custom'} />

	{#if !ready}
		{#key trouble?.tone + blocked}
			{#if trouble}
				<ScannerNotice {...trouble}>
					{#if onfound && trouble.tone === 'warning' && canLook}
						<FindScannersButton {onfound} />
					{/if}
				</ScannerNotice>
			{:else}
				<!-- Our own words: the server's for these ("Not checked yet.") are not translated. -->
				<p
					class="max-w-[420px] text-center text-[13px] font-semibold text-ink-muted"
					data-testid="scanner-trouble"
				>
					{blocked}
				</p>
			{/if}
		{/key}
	{/if}

	{#if canPickDevice}
		<div class="flex flex-col items-center gap-2" data-testid="device-picker">
			<span class="text-[13px] font-semibold text-ink-muted">{t('home.scanner')}</span>
			<!-- The look button sits beside the pills, so it adds no row to a full stage. -->
			<div class="flex items-center gap-1">
				<div
					class="flex flex-wrap justify-center gap-1.5 rounded-[24px] bg-ctl-track p-[4px]"
					role="radiogroup"
					aria-label={t('home.scanner')}
					tabindex="-1"
					bind:this={pills}
					onkeydown={rove}
					data-testid="device-pills"
				>
					{#each listed as scanner (scanner.Id)}
						{@const lit = shown === scanner.Id}
						<div
							class="relative"
							role="presentation"
							onmouseenter={() => {
								if (lit && details === null) details = 'hover';
							}}
							onmouseleave={() => {
								if (details === 'hover') details = null;
							}}
						>
							<button
								type="button"
								role="radio"
								class="flex h-[38px] max-w-[min(340px,80*var(--vw))] items-center rounded-full px-3.5 text-[13px] font-bold transition-[background-color,color,box-shadow,transform] duration-200 ease-spring active:scale-[0.97] {lit
									? 'bg-seg text-ink shadow-seg'
									: 'text-ink-2 hover:text-ink'}"
								aria-checked={lit}
								tabindex={lit ? 0 : -1}
								onclick={() => {
									if (lit) details = details === 'pinned' ? null : 'pinned';
									else settings.set({ ...$settings, Device: scanner.Id });
								}}
								data-device={scanner.Id}
								{@attach bounce(scanner.Id)}
							>
								<span class="truncate">{scanner.Name}</span>
							</button>
							{#if lit && details !== null}
								<ScannerDetails
									{status}
									{connection}
									name={scanner.Name}
									pinned={details === 'pinned'}
									onclose={() => (details = null)}
								/>
							{/if}
						</div>
					{/each}
				</div>
				{#if onfound && canLook}
					<FindScannersButton {onfound} compact />
				{/if}
			</div>
		</div>
	{/if}

	<PresetPills
		presets={offered}
		active={chosen?.id ?? null}
		edited={edited?.id ?? null}
		modified={chosen === null}
		onpick={(id) => {
			const preset = offered.find((p) => p.id === id);
			if (!preset) return;
			settings.set(applyPreset(preset, $settings));
			pickedPreset.set({ id });
		}}
		onsave={onsavepreset ?? onsettings}
	/>

	<SettingsChips
		{options}
		deviceName={canPickDevice ? null : deviceName}
		format={exportFormatLabels[$exportPrefs.format]}
		onclick={onsettings}
	/>

	<button
		type="button"
		onclick={onsettings}
		class="flex h-11 w-11 items-center justify-center rounded-full text-accent transition-colors hover:bg-accent-wash"
		aria-label={t('home.changeSettings')}
		title={t('home.changeSettings')}
		data-testid="change-settings"
	>
		<Icon name="sliders" />
	</button>

	<ScanButton disabled={!ready || busy} {busy} reason={blocked} onclick={onscan} />
</section>
