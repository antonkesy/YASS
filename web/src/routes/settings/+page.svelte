<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { hubError } from '$lib/hub/errors';
	import { getHub } from '$lib/hub/hub';
	import { PageSize, ScanSource } from '$lib/hub/generated/Yass.Core.Domain';
	import type { ScannerDeviceDto, ScanOptionsDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { editableKeys, scanOverrides, settings, type ScanOverrides } from '$lib/settings';
	import Segmented from '$lib/components/Segmented.svelte';
	import Toggle from '$lib/components/Toggle.svelte';
	import SettingsSection from '$lib/components/SettingsSection.svelte';
	import Slider from '$lib/components/Slider.svelte';
	import TemplateEditor from '$lib/components/TemplateEditor.svelte';
	import { Icon, presetIcons, type PresetIconName } from '$lib/icons';
	import {
		activePreset,
		applyPreset,
		exportPrefs,
		historyPrefs,
		pickedPreset,
		presets,
		reviewPrefs,
		type Preset,
		type RetentionUnit
	} from '$lib/presets';
	import { retentionUnits } from '$lib/sessions/retention';
	import { TILE_SIZES } from '$lib/review/overview';
	import { ExportFormat } from '$lib/hub/generated/Yass.Core.Hub';
	import { colorLabel, exportFormatLabels, pageSizeLabel, sourceLabel } from '$lib/format';
	import { fileExtensions } from '$lib/review/download';
	import { expandTemplate, templateTokens, unknownTokens } from '$lib/template';
	import { colorModeLabels } from '$lib/review/edits';

	const client = getHub();
	const connection = client.connection;

	let effective = $state<ScanOptionsDto | null>(null);
	let error = $state<string | null>(null);
	/** Scanners the server can see. Null until asked; empty means it found none. */
	let devices = $state<ScannerDeviceDto[] | null>(null);

	$effect(() => {
		if ($connection === 'connected' && !effective) {
			client.hub
				.getEffectiveOptions()
				.then((o) => (effective = o))
				.catch((e) => (error = hubError(e)));
			// Separately, and without failing the sheet: enumerating SANE takes seconds, and a
			// settings page that will not open because a scanner is slow is worse than one with
			// no picker on it.
			client.hub
				.listDevices()
				.then((d) => (devices = d))
				.catch(() => (devices = []));
		}
	});

	/**
	 * The overview's zoom steps as a row of choices. Named rather than shown in pixels: the
	 * number means nothing to anyone, and there are only ever a handful of steps.
	 */
	const tileSizes = [
		{ value: 'fit', label: 'Fit' },
		...TILE_SIZES.map((size, i) => ({
			value: String(size),
			label: ['Tiny', 'Small', 'Medium', 'Large', 'Huge'][i] ?? String(size)
		}))
	];

	const overrides = $derived($settings);
	const changedCount = $derived(
		Object.values(editableKeys).filter((key) => overrides[key] !== undefined).length
	);
	const changed = $derived(changedCount > 0);

	/**
	 * What "Default" stands for, said beside the control: a row of segments with Default lit
	 * otherwise leaves you guessing whether the next scan is 150 dpi or 600.
	 */
	const defaultHint = (label: string | undefined) => (label ? `default ${label}` : '');

	// "" is the segment that clears the override and falls back to yass.yaml.
	const DEFAULT = '';
	const asSegment = (v: number | boolean | undefined) => (v === undefined ? DEFAULT : String(v));

	const sources = [
		{ value: String(ScanSource.Flatbed), label: 'Flatbed' },
		{ value: String(ScanSource.Feeder), label: 'Feeder' },
		{ value: String(ScanSource.Duplex), label: 'Both sides' }
	];
	const colors = Object.entries(colorModeLabels).map(([value, label]) => ({ value, label }));
	const dpis = [150, 200, 300, 400, 600].map((d) => ({ value: String(d), label: String(d) }));
	const pageSizes = [
		{ value: String(PageSize.A4), label: 'A4' },
		{ value: String(PageSize.A5), label: 'A5' },
		{ value: String(PageSize.Letter), label: 'Letter' },
		{ value: String(PageSize.Legal), label: 'Legal' }
	];
	const withDefault = (options: { value: string; label: string }[]) => [
		{ value: DEFAULT, label: 'Default' },
		...options
	];

	/**
	 * What the next scan will do, over the keys this sheet can set: the server's defaults with
	 * this browser's overrides on top. Presets are matched against it and saved from it — the
	 * same thing the home screen matches. Matching the bare overrides had this sheet saying
	 * "Unsaved changes" while the home screen, one click away, lit up Document: a browser with
	 * nothing overridden still runs whichever preset yass.yaml defaults to. And a preset saved
	 * from bare overrides could pin nothing at all, which is a preset that matches no scan.
	 */
	const planned = $derived<ScanOverrides>(
		effective ? { ...scanOverrides(effective), ...$settings } : $settings
	);
	const chosen = $derived(activePreset($presets, planned, $pickedPreset.id));
	/**
	 * The preset picked last, once a setting has been changed away from it. Shown as edited
	 * rather than simply going dark, so it is clear what Update would overwrite.
	 */
	const edited = $derived(
		chosen ? null : ($presets.find((p) => p.id === $pickedPreset.id) ?? null)
	);
	/** The preset the Update, Delete and scope controls act on. */
	const target = $derived(chosen ?? edited);
	let drafting = $state(false);
	let draftName = $state('');
	let draftIcon = $state<PresetIconName>('star');
	/**
	 * Whether the new preset should be offered on every scanner. Off by default: presets
	 * follow the scanner picked on the home screen, and one saved for the flatbed rarely
	 * makes sense on the feeder next to it.
	 */
	let draftShared = $state(false);

	/** A device id as its human name, falling back to the id for one the server no longer lists. */
	const deviceLabel = (id: string) => devices?.find((d) => d.Id === id)?.Name ?? id;

	/**
	 * The scanner the next scan runs on, chosen on the home screen. Empty when none is —
	 * and then a preset has nothing to belong to, so it is offered everywhere.
	 */
	const currentDevice = $derived($settings.Device || effective?.Device || '');

	const previewFacts = $derived({
		preset: chosen?.name.replace(/\s+/g, '-') ?? 'Custom',
		counter: 42,
		dpi: $settings.Dpi ?? effective?.Dpi ?? 300,
		color: colorModeLabels[$settings.Color ?? effective?.Color ?? 2] ?? 'Color'
	});
	const templatePreview = $derived(
		$exportPrefs.nameTemplate.trim()
			? expandTemplate($exportPrefs.nameTemplate, new Date(), previewFacts)
			: ''
	);

	/**
	 * Whole templates worth starting from, each shown as what it would produce. The examples
	 * are expanded with the same facts as the preview, so what the menu promises is what the
	 * field will show once it is picked.
	 */
	const templatePresets = $derived(
		['{YEAR}-{MONTH}-{DAY}', '{YEAR}-{MONTH}-{DAY}_{HOUR}{MINUTE}', '{PRESET}-{COUNTER}'].map(
			(template) => ({
				template,
				example: expandTemplate(template, new Date(), previewFacts)
			})
		)
	);
	const templateUnknown = $derived(unknownTokens($exportPrefs.nameTemplate));

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

	function resetSettings() {
		settings.reset();
		pickedPreset.set({ id: null });
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

	function close() {
		void goto(resolve('/'));
	}

	function setNumber<K extends 'Source' | 'Dpi' | 'Color' | 'PageSize'>(key: K, raw: string) {
		settings.update(key, raw === DEFAULT ? undefined : (Number(raw) as never));
	}
</script>

<svelte:head><title>Settings · YASS</title></svelte:head>

<!--
	The route stays, but it reads as the mockup's sheet: a centred card on a desktop
	and a bottom sheet on a phone, over a dimmed page.
-->
<div class="fixed inset-0 z-40 bg-[rgb(19_26_43_/_0.35)] backdrop-blur-[3px]"></div>

<!--
	Header and footer stay put while the middle scrolls, so Close, Reset and Done are never
	a long scroll away. The card only clips; the wrapper inside it scrolls, a little in from
	the edge, so the scrollbar does not run through the rounded corners.
-->
<div
	class="fixed inset-x-0 bottom-0 z-41 flex max-h-[92%] animate-pop-in flex-col overflow-hidden rounded-t-[28px] bg-surface shadow-modal sm:inset-x-auto sm:top-1/2 sm:bottom-auto sm:left-1/2 sm:max-h-[calc(100%-32px)] sm:w-[min(1080px,calc(100%-40px))] sm:-translate-x-1/2 sm:-translate-y-1/2 sm:rounded-[26px]"
	role="dialog"
	aria-modal="true"
	aria-label="Settings"
>
	<header class="flex-none border-b border-line px-[22px] pt-3 pb-4 sm:px-[34px] sm:pt-6">
		<span class="mx-auto mb-3 block h-1 w-10 rounded-full bg-handle sm:hidden" aria-hidden="true"
		></span>
		<div class="flex items-center gap-2">
			<div class="flex min-w-0 flex-col">
				<h1 class="text-[20px] font-extrabold tracking-[-0.01em] sm:text-[24px]">Settings</h1>
				<p class="text-[12px] leading-snug text-ink-muted sm:text-[13px]">
					Kept in this browser. Default means whatever
					<code class="font-mono">yass.yaml</code> says.
				</p>
			</div>
			<div class="flex-1"></div>
			{#if changed}
				<span
					class="flex h-7 items-center gap-1.5 rounded-full bg-accent-tint px-2.5 text-[12px] font-bold whitespace-nowrap text-accent-deep"
					title="Scan options changed from the defaults for the next scan"
					data-testid="overrides-active"
				>
					<span class="h-1.5 w-1.5 rounded-full bg-accent"></span>
					{changedCount} changed
				</span>
			{/if}
			<button
				type="button"
				class="h-9 rounded-full px-3 text-[13px] font-bold text-accent transition-colors hover:bg-accent-wash disabled:pointer-events-none disabled:text-ink-faint"
				onclick={resetSettings}
				disabled={!changed}
				title="Put every scan option back to its default"
				data-testid="reset-settings"
			>
				Reset
			</button>
			<button
				type="button"
				class="flex h-11 w-11 flex-none items-center justify-center rounded-full bg-ctl transition-colors hover:bg-ctl-hover"
				onclick={close}
				aria-label="Close settings"
			>
				<Icon name="close" />
			</button>
		</div>
	</header>

	<div
		class="grid min-h-0 flex-1 items-start gap-y-5 overflow-y-auto overscroll-contain px-[22px] py-5 sm:mr-2.5 sm:pr-[24px] sm:pl-[34px] md:py-7"
	>
		<SettingsSection
			title="Scanner"
			icon="scan"
			hint="What the next scan asks the scanner for."
			testid="scanner-section"
		>
			{#if error}
				<p
					class="col-span-full rounded-[14px] bg-danger-bg px-4 py-2 font-semibold text-danger"
					role="alert"
				>
					{error}
				</p>
			{:else if !effective}
				<div class="col-span-full grid gap-3" aria-label="Loading" role="status">
					{#each [0, 1, 2] as i (i)}
						<div class="h-[60px] animate-pulse rounded-[14px] bg-skeleton"></div>
					{/each}
				</div>
			{:else}
				<div
					class="col-span-full flex flex-col gap-3 rounded-[18px] bg-app p-3.5"
					data-testid="presets"
				>
					<div class="flex items-center gap-2">
						<span class="text-[13px] font-bold text-ink-2">Presets</span>
						{#if chosen}
							<span
								class="flex items-center gap-1 text-xs font-bold text-success-ink"
								data-testid="preset-match"
							>
								<Icon name="checkSmall" />
								{chosen.name}
							</span>
						{:else if edited}
							<span class="text-xs font-bold text-warning-ink" data-testid="preset-edited">
								“{edited.name}” edited
							</span>
						{:else}
							<span class="text-xs font-bold text-warning-ink">Custom</span>
						{/if}
					</div>

					<div class="flex flex-wrap gap-1.5">
						{#each $presets as preset (preset.id)}
							<button
								type="button"
								class="relative flex h-[38px] items-center gap-2 rounded-full px-3.5 text-[13px] font-bold transition-all duration-200 {chosen?.id ===
								preset.id
									? 'bg-accent text-white shadow-preset'
									: edited?.id === preset.id
										? 'bg-surface text-accent-deep shadow-ring-outline'
										: 'bg-surface text-accent-deep shadow-chip hover:-translate-y-px hover:bg-accent-50'}"
								aria-pressed={chosen?.id === preset.id}
								title={edited?.id === preset.id ? 'Edited — press to go back to it' : undefined}
								onclick={() => pickPreset(preset)}
							>
								{#if edited?.id === preset.id}
									<span
										class="absolute top-0.5 right-1 h-2 w-2 rounded-full bg-warning ring-2 ring-surface"
										aria-hidden="true"
									></span>
								{/if}
								<Icon name={preset.icon} />
								{preset.name}
								<!-- The home screen only offers this one on that scanner, so this sheet — the
								     one place every preset is listed — has to say which. -->
								{#if preset.device}
									<span class="max-w-[110px] truncate text-[11px] font-semibold opacity-65"
										>{deviceLabel(preset.device)}</span
									>
								{/if}
							</button>
						{/each}
						{#if !drafting}
							<button
								type="button"
								class="flex h-[38px] items-center gap-1.5 rounded-full border-[1.5px] border-dashed border-accent-dash px-3.5 text-[13px] font-bold text-accent transition-colors hover:border-accent hover:bg-surface"
								onclick={() => (drafting = true)}
								data-testid="preset-new"
							>
								<Icon name="plus" />
								Save as new
							</button>
						{/if}
					</div>

					{#if drafting}
						<div
							class="flex animate-pop-in flex-col gap-3 rounded-[14px] bg-surface p-3 shadow-chip"
						>
							<div class="flex flex-wrap gap-2">
								<input
									class="field h-10 min-w-[160px] flex-1 px-3 text-sm"
									type="text"
									placeholder="Preset name"
									bind:value={draftName}
									onkeydown={(e) => e.key === 'Enter' && savePreset()}
									data-testid="preset-name"
								/>
								<button
									type="button"
									class="h-10 rounded-full px-3.5 text-[13px] font-bold text-ink-3 transition-colors hover:bg-ctl"
									onclick={() => (drafting = false)}
								>
									Cancel
								</button>
								<button
									type="button"
									class="h-10 rounded-full bg-accent px-4 text-[13px] font-extrabold text-white transition-[opacity,background-color] hover:bg-accent-hover disabled:pointer-events-none disabled:opacity-45"
									disabled={!draftName.trim()}
									onclick={savePreset}
									data-testid="preset-save"
								>
									Save
								</button>
							</div>
							<div class="flex flex-wrap gap-1.5" role="group" aria-label="Icon">
								{#each presetIcons as icon (icon)}
									<button
										type="button"
										class="flex h-9 w-9 items-center justify-center rounded-[10px] transition-transform duration-150 hover:scale-110 {draftIcon ===
										icon
											? 'bg-accent text-white shadow-swatch'
											: 'bg-ctl text-ink-2'}"
										aria-pressed={draftIcon === icon}
										aria-label={icon}
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
									Available for all scanners
									<span class="font-normal text-ink-muted"
										>· otherwise only on {deviceLabel(currentDevice)}</span
									>
								</label>
							{/if}
						</div>
					{:else if target}
						<div
							class="flex flex-wrap items-center gap-x-3 gap-y-1.5 border-t border-line-strong pt-2.5"
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
									Available for all scanners
									<span class="font-normal text-ink-muted"
										>· otherwise only on {deviceLabel(target.device || currentDevice)}</span
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
									Revert
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
								Update “{target.name}”
							</button>
							<button
								type="button"
								class="flex h-8 items-center gap-1 rounded-full px-3 text-xs font-bold text-danger transition-colors hover:bg-danger-bg"
								onclick={() => deletePreset(target.id)}
							>
								<Icon name="trash" />
								Delete
							</button>
						</div>
					{/if}
				</div>

				<form
					class="col-span-full grid items-start gap-x-6 gap-y-5 sm:grid-cols-2"
					onsubmit={(e) => e.preventDefault()}
					data-testid="settings-form"
				>
					<Segmented
						name="source"
						label="Source"
						hint={defaultHint(sourceLabel(effective.Source))}
						options={withDefault(sources)}
						value={asSegment(overrides.Source)}
						onchange={(v) => setNumber('Source', v)}
					/>
					<Segmented
						name="color"
						label="Colour mode"
						hint={defaultHint(colorLabel(effective.Color))}
						options={withDefault(colors)}
						value={asSegment(overrides.Color)}
						onchange={(v) => setNumber('Color', v)}
					/>
					<Segmented
						name="dpi"
						label="Resolution"
						hint={effective.Dpi ? `default ${effective.Dpi} dpi` : 'dpi'}
						options={withDefault(dpis)}
						value={asSegment(overrides.Dpi)}
						onchange={(v) => setNumber('Dpi', v)}
					/>
					<Segmented
						name="page_size"
						label="Paper size"
						hint={defaultHint(pageSizeLabel(effective.PageSize))}
						options={withDefault(pageSizes)}
						value={asSegment(overrides.PageSize)}
						onchange={(v) => setNumber('PageSize', v)}
					/>

					<!-- What happens to a page after it comes off the scanner. -->
					<fieldset class="flex flex-col rounded-[16px] bg-app px-3.5 py-1.5">
						<legend class="sr-only">Clean-up</legend>
						<Toggle
							name="blank_page_detection"
							label="Skip blank pages"
							hint="Pages that look empty start out deleted"
							checked={overrides.BlankPageDetection ?? effective.BlankPageDetection ?? false}
							onchange={(v) => settings.update('BlankPageDetection', v)}
						/>
						<!-- Belongs to the toggle above, and only matters while it is on. -->
						<label
							class="mb-2 ml-3 flex items-center justify-between gap-3 border-l-2 border-line-strong pl-3 transition-opacity {(overrides.BlankPageDetection ??
							effective.BlankPageDetection)
								? ''
								: 'opacity-50'}"
						>
							<span class="flex min-w-0 flex-col">
								<span class="text-[13px] font-bold text-ink-2">Blank threshold</span>
								<span class="text-xs text-ink-muted">
									Higher skips more · default {effective.BlankPageThreshold}
								</span>
							</span>
							<input
								class="field h-10 w-[84px] flex-none px-2.5 text-center font-mono text-sm"
								type="number"
								name="blank_page_threshold"
								min="0"
								max="1"
								step="0.01"
								value={overrides.BlankPageThreshold ?? ''}
								placeholder={String(effective.BlankPageThreshold)}
								onchange={(e) => {
									const v = (e.currentTarget as HTMLInputElement).value;
									settings.update('BlankPageThreshold', v === '' ? undefined : Number(v));
								}}
							/>
						</label>
						<div class="border-t border-line-strong"></div>
						<Toggle
							name="auto_deskew"
							label="Auto-deskew"
							hint="Straighten pages that went through crooked"
							checked={overrides.AutoDeskew ?? effective.AutoDeskew ?? false}
							onchange={(v) => settings.update('AutoDeskew', v)}
						/>
					</fieldset>

					<fieldset class="flex flex-col gap-1 rounded-[16px] bg-app px-3.5 py-2.5">
						<legend class="sr-only">Image adjustments</legend>
						{#each [['brightness', 'Brightness', 'Brightness'], ['contrast', 'Contrast', 'Contrast']] as const as [key, label, prop] (key)}
							<Slider
								name={key}
								{label}
								value={overrides[prop] ?? effective[prop] ?? 0}
								onchange={(v) => settings.update(prop, v)}
							/>
						{/each}
					</fieldset>
				</form>
			{/if}
		</SettingsSection>

		<!--
			Outside the scanner section above, and for the same reason: what kind of file a
			document is and what it is called are this browser's business. A server that lets
			the UI change no scan setting still has to let you name what comes off it.
		-->
		<SettingsSection
			title="Export"
			icon="download"
			hint="What kind of file a document is downloaded as."
			testid="export-section"
		>
			<Segmented
				name="format"
				label="File format"
				hint={$exportPrefs.format === ExportFormat.Pdf
					? 'one file per document'
					: 'one file per page'}
				options={[
					{ value: String(ExportFormat.Pdf), label: exportFormatLabels[ExportFormat.Pdf] },
					{ value: String(ExportFormat.Jpg), label: exportFormatLabels[ExportFormat.Jpg] },
					{ value: String(ExportFormat.Png), label: exportFormatLabels[ExportFormat.Png] }
				]}
				value={String($exportPrefs.format)}
				onchange={(v) => exportPrefs.set({ ...$exportPrefs, format: Number(v) as ExportFormat })}
			/>
		</SettingsSection>

		<!--
			Outside the scanner section above: these are preferences of this browser's, with
			nothing to do with how the scanner is driven, and they have to stay reachable
			while that section is still loading or has failed.
		-->
		<SettingsSection
			title="Naming"
			icon="pencil"
			hint="What a document is called when nobody has named it."
			testid="naming-section"
		>
			<div class="col-span-full flex flex-col gap-1.5">
				<span class="text-[13px] font-bold text-ink-2">File name template</span>
				<TemplateEditor
					value={$exportPrefs.nameTemplate}
					tokens={templateTokens}
					preview={templatePreview}
					presets={templatePresets}
					unknown={templateUnknown}
					extension={fileExtensions[$exportPrefs.format]}
					onchange={(v) => exportPrefs.set({ ...$exportPrefs, nameTemplate: v })}
				/>
			</div>
		</SettingsSection>

		<!--
			What a PDF says about itself in its properties. Kept in this browser like the rest
			of the export preferences: it is about whose documents these are, not how they scan.
		-->
		<SettingsSection
			title="Metadata"
			icon="tag"
			hint="Written into every PDF's properties. The title is always the document's name."
			testid="metadata-section"
		>
			{#each [['author', 'Author', 'Who the documents belong to', 'Left empty, the PDF names nobody.'], ['subject', 'Subject', 'What they are about', 'If every scan from here is about the same thing.'], ['keywords', 'Keywords', 'invoices, tax, 2026', 'Comma-separated, for whatever indexes them.']] as const as [key, label, placeholder, hint] (key)}
				<label class="flex flex-col gap-1.5">
					<span class="text-[13px] font-bold text-ink-2">{label}</span>
					<input
						class="field h-11 px-3 text-sm placeholder:text-ink-faint"
						type="text"
						name="metadata-{key}"
						autocomplete="off"
						{placeholder}
						value={$exportPrefs[key]}
						oninput={(e) => exportPrefs.set({ ...$exportPrefs, [key]: e.currentTarget.value })}
						data-testid="metadata-{key}"
					/>
					<span class="text-[11px] text-ink-faint">{hint}</span>
				</label>
			{/each}
		</SettingsSection>

		<!--
			The one preference the review screen keeps, and it had nowhere to be said out loud:
			the overview's own zoom buttons set it, which you only find by opening the overview.
		-->
		<SettingsSection
			title="Editing"
			icon="grid"
			hint="How the review screen behaves while you go through the pages."
			testid="editing-section"
		>
			<div class="col-span-full flex flex-col gap-1.5">
				<Segmented
					name="overview-tile"
					label="Page size in the all-pages view"
					options={tileSizes}
					value={String($reviewPrefs.overviewTile)}
					onchange={(v) =>
						reviewPrefs.set({ ...$reviewPrefs, overviewTile: v === 'fit' ? 'fit' : Number(v) })}
				/>
				<span class="text-[11px] text-ink-faint">
					Fit puts the whole scan on one screen; the zoom buttons in that view set the rest.
				</span>
			</div>
		</SettingsSection>

		<!--
			Past scans live on the server, but when they stop being wanted is this browser's
			call: whichever browser has this set deletes them as they age out.
		-->
		<SettingsSection
			title="Past scans"
			icon="archive"
			hint="Scans stay listed until you delete them, or until this does."
			testid="history-section"
		>
			<div class="col-span-full flex flex-col rounded-[16px] bg-app px-3.5 py-1.5">
				<Toggle
					name="auto-clear"
					label="Delete old scans automatically"
					hint="Whichever browser has this on does the deleting"
					checked={$historyPrefs.autoClear !== null}
					onchange={(on) =>
						historyPrefs.set({ autoClear: on ? { amount: 30, unit: 'days' } : null })}
				/>
				{#if $historyPrefs.autoClear}
					{@const rule = $historyPrefs.autoClear}
					<label
						class="mb-2 flex animate-fade-in flex-wrap items-center gap-2 border-t border-line-strong pt-2.5 text-[13px] font-bold text-ink-2"
					>
						Older than
						<input
							class="field h-10 w-16 px-2.5 text-center text-sm tabular-nums"
							type="number"
							name="auto-clear-amount"
							min="1"
							step="1"
							value={rule.amount}
							oninput={(e) => {
								const amount = Math.max(1, Math.floor(Number(e.currentTarget.value) || 1));
								historyPrefs.set({ autoClear: { ...rule, amount } });
							}}
							data-testid="auto-clear-amount"
						/>
						<select
							class="field h-10 px-2.5 text-sm"
							name="auto-clear-unit"
							value={rule.unit}
							onchange={(e) =>
								historyPrefs.set({
									autoClear: { ...rule, unit: e.currentTarget.value as RetentionUnit }
								})}
							data-testid="auto-clear-unit"
						>
							{#each retentionUnits as unit (unit)}
								<option value={unit}>{unit}</option>
							{/each}
						</select>
					</label>
				{/if}
			</div>
		</SettingsSection>
	</div>

	<footer
		class="flex flex-none justify-end border-t border-line px-[22px] pt-3.5 pb-[max(1.25rem,env(safe-area-inset-bottom))] sm:px-[34px] sm:pb-5"
	>
		<button
			type="button"
			class="h-12 w-full rounded-[14px] bg-linear-135 from-accent-light to-accent-grad2 text-[15px] font-extrabold text-white shadow-done transition-shadow duration-200 hover:shadow-export-hover sm:w-[200px]"
			onclick={close}
		>
			Done
		</button>
	</footer>
</div>
