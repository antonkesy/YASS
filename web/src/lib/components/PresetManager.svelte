<script lang="ts">
	import { slide } from 'svelte/transition';
	import { page } from '$app/state';
	import { t, type MessageKey } from '$lib/i18n/index.svelte';
	import { Icon, presetIcons, type PresetIconName } from '$lib/icons';
	import PresetPill from './PresetPill.svelte';
	import { ms } from '$lib/motion';
	import { applyPreset, pickedPreset, presetName, presets, type Preset } from '$lib/presets';
	import { settings, type ScanOverrides } from '$lib/settings';

	let {
		planned,
		chosen,
		currentDevice,
		deviceLabel
	}: {
		/**
		 * What the next scan will do: the server's defaults with this browser's overrides on
		 * top. Presets are saved from it.
		 */
		planned: ScanOverrides;
		/** The preset the next scan is running, if any. */
		chosen: Preset | null;
		/**
		 * The scanner the next scan runs on, chosen on the home screen. Empty when none is —
		 * and then a preset has nothing to belong to, so it is offered everywhere.
		 */
		currentDevice: string;
		/** A device id as its human name. */
		deviceLabel: (id: string) => string;
	} = $props();

	/** The preset icons' names, for whoever hears the icon picker rather than sees it. */
	const iconNames: Record<PresetIconName, MessageKey> = {
		star: 'settings.iconStar',
		file: 'settings.iconFile',
		copy: 'settings.iconCopy',
		receipt: 'settings.iconReceipt',
		image: 'settings.iconImage',
		briefcase: 'settings.iconBriefcase',
		heart: 'settings.iconHeart',
		home: 'settings.iconHome',
		book: 'settings.iconBook',
		mail: 'settings.iconMail',
		tag: 'settings.iconTag',
		zap: 'settings.iconZap'
	};

	/**
	 * The preset picked last, once a setting has been changed away from it. Shown as edited
	 * rather than simply going dark, so it is clear what Update would overwrite.
	 */
	const edited = $derived(
		chosen ? null : ($presets.find((p) => p.id === $pickedPreset.id) ?? null)
	);
	/** The preset the Update, Delete and scope controls act on. */
	const target = $derived(chosen ?? edited);
	/** Arriving from the home screen's Save as preset opens the name field straight away. */
	let drafting = $state(page.state.newPreset === true);
	let draftName = $state('');
	let draftIcon = $state<PresetIconName>('star');
	/**
	 * Whether the new preset should be offered on every scanner. Off by default: presets
	 * follow the scanner picked on the home screen, and one saved for the flatbed rarely
	 * makes sense on the feeder next to it.
	 */
	let draftShared = $state(false);

	/**
	 * A preset never carries the scanner in its settings: the scanner is picked on the home
	 * screen and the presets follow it, so one that moved the machine when it was pressed
	 * would be arguing with the control right above it.
	 */
	function withoutDevice(overrides: ScanOverrides): ScanOverrides {
		const rest = { ...overrides };
		delete rest.Device;
		return rest;
	}

	function savePreset() {
		const name = draftName.trim();
		if (!name) return;
		const preset: Preset = {
			id: `${Date.now().toString(36)}-${name.toLowerCase().replace(/[^a-z0-9]+/g, '-')}`,
			name,
			icon: draftIcon,
			settings: withoutDevice(planned),
			...(draftShared || !currentDevice ? {} : { device: currentDevice })
		};
		presets.set([...$presets, preset]);
		pickedPreset.set({ id: preset.id });
		drafting = false;
		draftName = '';
		draftShared = false;
	}

	function pickPreset(preset: Preset) {
		settings.set(applyPreset(preset, $settings));
		pickedPreset.set({ id: preset.id });
	}

	function updatePreset() {
		if (!target) return;
		const id = target.id;
		presets.set(
			$presets.map((p) => (p.id === id ? { ...p, settings: withoutDevice(planned) } : p))
		);
		pickedPreset.set({ id });
	}

	/** Move a saved preset between "every scanner" and "only the one chosen now". */
	function sharePreset(id: string, shared: boolean) {
		presets.set(
			$presets.map((p) => {
				if (p.id !== id) return p;
				const next = { ...p };
				if (shared) delete next.device;
				else next.device = currentDevice;
				return next;
			})
		);
	}

	function deletePreset(id: string) {
		presets.set($presets.filter((p) => p.id !== id));
		if ($pickedPreset.id === id) pickedPreset.set({ id: null });
	}
</script>

<div class="col-span-full flex flex-col gap-3 rounded-[18px] bg-app p-3.5" data-testid="presets">
	<div class="flex items-center gap-2">
		<span class="text-[13px] font-bold text-ink-2">{t('settings.presets')}</span>
		{#if chosen}
			<span
				class="flex items-center gap-1 text-xs font-bold text-success-ink"
				data-testid="preset-match"
			>
				<Icon name="checkSmall" />
				{presetName(chosen)}
			</span>
		{:else if edited}
			<span class="text-xs font-bold text-warning-ink" data-testid="preset-edited">
				{t('settings.presetEdited', { name: presetName(edited) })}
			</span>
		{:else}
			<span class="text-xs font-bold text-warning-ink">{t('settings.custom')}</span>
		{/if}
	</div>

	<div class="flex flex-wrap gap-1.5">
		{#each $presets as preset (preset.id)}
			<!-- The home screen only offers one with a scanner on that scanner, so this sheet — the
			     one place every preset is listed — has to say which. -->
			<PresetPill
				{preset}
				active={chosen?.id === preset.id}
				edited={edited?.id === preset.id}
				editedTitle={t('settings.presetEditedTitle')}
				note={preset.device ? deviceLabel(preset.device) : ''}
				onclick={() => pickPreset(preset)}
			/>
		{/each}
		{#if !drafting}
			<button
				type="button"
				class="flex h-[38px] items-center gap-1.5 rounded-full border-[1.5px] border-dashed border-accent-dash px-3.5 text-[13px] font-bold text-accent transition-colors hover:border-accent hover:bg-surface"
				onclick={() => (drafting = true)}
				data-testid="preset-new"
			>
				<Icon name="plus" />
				{t('settings.saveAsNew')}
			</button>
		{/if}
	</div>

	{#if drafting}
		<div
			class="flex flex-col gap-3 rounded-[14px] bg-surface p-3 shadow-chip"
			transition:slide={{ duration: ms(220) }}
		>
			<div class="flex flex-wrap gap-2">
				<input
					class="field h-10 min-w-[160px] flex-1 px-3 text-sm"
					type="text"
					placeholder={t('settings.presetNamePlaceholder')}
					bind:value={draftName}
					onkeydown={(e) => e.key === 'Enter' && savePreset()}
					data-testid="preset-name"
					{@attach (el) => el.focus()}
				/>
				<button
					type="button"
					class="h-10 rounded-full px-3.5 text-[13px] font-bold text-ink-3 transition-colors hover:bg-ctl"
					onclick={() => (drafting = false)}
				>
					{t('settings.cancel')}
				</button>
				<button
					type="button"
					class="h-10 rounded-full bg-accent px-4 text-[13px] font-extrabold text-white transition-[opacity,background-color] hover:bg-accent-hover disabled:pointer-events-none disabled:opacity-45"
					disabled={!draftName.trim()}
					onclick={savePreset}
					data-testid="preset-save"
				>
					{t('settings.save')}
				</button>
			</div>
			<div class="flex flex-wrap gap-1.5" role="group" aria-label={t('settings.icon')}>
				{#each presetIcons as icon (icon)}
					<button
						type="button"
						class="flex h-9 w-9 items-center justify-center rounded-[10px] transition-transform duration-150 hover:scale-110 {draftIcon ===
						icon
							? 'bg-accent text-white shadow-swatch'
							: 'bg-ctl text-ink-2'}"
						aria-pressed={draftIcon === icon}
						aria-label={t(iconNames[icon])}
						onclick={() => (draftIcon = icon)}
					>
						<Icon name={icon} />
					</button>
				{/each}
			</div>
			{#if currentDevice}
				<!-- Only worth asking when there is a scanner for it to belong to. -->
				<label class="flex flex-wrap items-center gap-2 text-xs font-semibold text-ink-2">
					<input
						type="checkbox"
						class="h-4 w-4 accent-accent"
						bind:checked={draftShared}
						data-testid="preset-shared"
					/>
					{t('settings.allScanners')}
					<span class="font-normal text-ink-muted"
						>{t('settings.onlyOn', { name: deviceLabel(currentDevice) })}</span
					>
				</label>
			{/if}
		</div>
	{:else if target}
		<div
			class="flex flex-wrap items-center gap-x-3 gap-y-1.5 border-t border-line-strong pt-2.5"
			transition:slide={{ duration: ms(220) }}
		>
			{#if currentDevice || target.device}
				<label class="flex flex-wrap items-center gap-2 text-xs font-semibold text-ink-2">
					<input
						type="checkbox"
						class="h-4 w-4 accent-accent"
						checked={!target.device}
						onchange={(e) => sharePreset(target.id, e.currentTarget.checked)}
						data-testid="preset-scope"
					/>
					{t('settings.allScanners')}
					<span class="font-normal text-ink-muted"
						>{t('settings.onlyOn', {
							name: deviceLabel(target.device || currentDevice)
						})}</span
					>
				</label>
			{/if}
			<div class="flex-1"></div>
			{#if edited}
				<button
					type="button"
					class="h-8 rounded-full px-3 text-xs font-bold text-ink-3 transition-colors hover:bg-ctl"
					onclick={() => pickPreset(edited)}
					data-testid="preset-revert"
				>
					{t('settings.revert')}
				</button>
			{/if}
			<button
				type="button"
				class="h-8 rounded-full px-3 text-xs font-bold transition-colors {edited
					? 'bg-accent text-white hover:bg-accent-hover'
					: 'text-accent hover:bg-accent-wash'}"
				onclick={updatePreset}
				data-testid="preset-update"
			>
				{t('settings.update', { name: presetName(target) })}
			</button>
			<button
				type="button"
				class="flex h-8 items-center gap-1 rounded-full px-3 text-xs font-bold text-danger transition-colors hover:bg-danger-bg"
				onclick={() => deletePreset(target.id)}
			>
				<Icon name="trash" />
				{t('settings.delete')}
			</button>
		</div>
	{/if}
</div>
