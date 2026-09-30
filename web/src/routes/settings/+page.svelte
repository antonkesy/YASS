<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { hubError } from '$lib/hub/errors';
	import { getHub } from '$lib/hub/hub';
	import { PageSize, ScanSource } from '$lib/hub/generated/Yass.Core.Domain';
	import type { ScannerDeviceDto, ScanOptionsDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { editableKeys, settings } from '$lib/settings';
	import Segmented from '$lib/components/Segmented.svelte';
	import Toggle from '$lib/components/Toggle.svelte';
	import ProgressBar from '$lib/components/ProgressBar.svelte';
	import SettingsSection from '$lib/components/SettingsSection.svelte';
	import TemplateEditor from '$lib/components/TemplateEditor.svelte';
	import { Icon, presetIcons, type PresetIconName } from '$lib/icons';
	import {
		activePreset,
		aiKeys,
		aiPrefs,
		exportPrefs,
		presets,
		reviewPrefs,
		type Preset
	} from '$lib/presets';
	import { TILE_SIZES } from '$lib/review/overview';
	import {
		chosenBaseUrl,
		chosenModel,
		CPU_MODELS,
		cpuModelLabel,
		cpuModelSizeMb,
		isInBrowser,
		knownBaseUrls,
		NamingError,
		suggestName,
		type AiProvider
	} from '$lib/naming';
	import { ExportFormat } from '$lib/hub/generated/Yass.Core.Hub';
	import { exportFormatLabels } from '$lib/format';
	import { fileExtensions } from '$lib/review/download';
	import { expandParts, expandTemplate, offeredTokens, unknownTokens } from '$lib/template';
	import { SAMPLE_NAME, styleName, type NameStyle } from '$lib/naming/style';
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

	const editable = $derived(new Set(effective?.Editable ?? []));

	/** Who writes the name, and what each choice needs filled in. */
	const providers: { value: AiProvider; label: string }[] = [
		{ value: 'off', label: 'Off' },
		{ value: 'anthropic', label: 'Anthropic' },
		{ value: 'openai', label: 'OpenAI-style' },
		{ value: 'webllm', label: 'In-browser (GPU)' },
		{ value: 'wllama', label: 'In-browser (CPU)' }
	];

	// WebGPU or nothing for the GPU engine, and it is worth saying which before someone picks
	// it. The CPU engine needs no such check: every browser has a processor (ADR-011).
	const canRunLocally = typeof navigator !== 'undefined' && navigator.gpu != null;
	let gpuModels = $state<{ id: string; sizeMb: number }[]>([]);
	/** How far the weights have got, 0-100, or null when nothing is downloading. */
	let downloading = $state<number | null>(null);

	// The GPU catalogue comes from the package, so its list and sizes cannot go stale here.
	// The CPU one is ours, pinned to commit shas so its sizes cannot either.
	$effect(() => {
		if (provider === 'webllm' && canRunLocally && gpuModels.length === 0) {
			void import('$lib/naming/webllm').then(async (m) => {
				gpuModels = await m.localModels();
			});
		}
	});

	async function download() {
		const model = chosenModel($aiPrefs);
		if (!model) return;
		testResult = null;
		downloading = 0;
		try {
			const engine =
				provider === 'wllama'
					? await import('$lib/naming/wllama')
					: await import('$lib/naming/webllm');
			await engine.loadModel(model, (_phase, percent) => (downloading = percent ?? downloading));
			// The CPU setting is a URL; say which model by name, the way the popover will.
			const named = provider === 'wllama' ? cpuModelLabel(model) : model;
			testResult = { ok: true, message: `${named} is ready, and stays on this machine.` };
		} catch (e) {
			testResult = {
				ok: false,
				message: e instanceof NamingError ? e.message : 'The model could not be loaded.'
			};
		} finally {
			downloading = null;
		}
	}
	const provider = $derived($aiPrefs.provider);
	const needsKey = $derived(provider === 'anthropic' || provider === 'openai');
	const key = $derived(provider === 'anthropic' ? $aiKeys.anthropic : $aiKeys.openai);

	const localModels = $derived(
		provider === 'wllama'
			? CPU_MODELS.map((m) => ({ id: m.url, label: m.label, sizeMb: cpuModelSizeMb(m.bytes) }))
			: gpuModels.map((m) => ({ id: m.id, label: m.id, sizeMb: m.sizeMb }))
	);

	/** The chosen model is a GGUF URL for CPU and a catalogue id for GPU; both live here. */
	const localModel = $derived($aiPrefs.models[provider] ?? '');

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

	/** What to call it while it downloads: the CPU setting is a URL, which reads as nothing. */
	const localLabel = $derived(provider === 'wllama' ? cpuModelLabel(localModel) : localModel);

	/** A pasted URL is not in the list, so the select shows nothing and the field holds it. */
	const isPasted = $derived(
		provider === 'wllama' && localModel !== '' && !CPU_MODELS.some((m) => m.url === localModel)
	);

	function setKey(value: string) {
		aiKeys.set(
			provider === 'anthropic' ? { ...$aiKeys, anthropic: value } : { ...$aiKeys, openai: value }
		);
	}

	function setModel(value: string) {
		aiPrefs.set({ ...$aiPrefs, models: { ...$aiPrefs.models, [provider]: value } });
	}

	/**
	 * One short request down the real path, because a special test endpoint would prove
	 * nothing about the one that matters. This is where a provider that refuses requests
	 * from web pages should be discovered, rather than at a scanner with a document in hand.
	 */
	const SAMPLE =
		'Northwind Traders GmbH\nQuarterly Report 2026 Q3\nPrepared for the board, 29 September 2026.';

	let testing = $state(false);
	let testResult = $state<{ ok: boolean; message: string } | null>(null);

	async function test() {
		testing = true;
		testResult = null;
		const started = performance.now();
		try {
			const named = await suggestName({ kind: 'text', text: SAMPLE, pages: 1 }, $aiPrefs, $aiKeys);
			const ms = Math.round(performance.now() - started);
			testResult = named
				? { ok: true, message: `${named.model} answered “${named.name}” · ${ms} ms` }
				: { ok: false, message: 'Nothing was asked: no model is set up.' };
		} catch (e) {
			testResult = {
				ok: false,
				message: e instanceof NamingError ? e.message : 'Something went wrong asking the model.'
			};
		} finally {
			testing = false;
		}
	}
	const overrides = $derived($settings);
	const changed = $derived(
		Object.keys(editableKeys).some(
			(k) => editable.has(k) && overrides[editableKeys[k]] !== undefined
		)
	);

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

	const chosen = $derived(activePreset($presets, $settings));
	let drafting = $state(false);
	let draftName = $state('');
	let draftIcon = $state<PresetIconName>('star');

	/** The shapes a found name can take, worded as the file name each one produces. */
	const nameStyles: { value: NameStyle; label: string }[] = [
		{ value: 'words', label: 'As written' },
		{ value: 'snake', label: 'lower_snake' },
		{ value: 'kebab', label: 'lower-kebab' }
	];

	const previewFacts = $derived({
		preset: chosen?.name.replace(/\s+/g, '-') ?? 'Custom',
		counter: 42,
		dpi: $settings.Dpi ?? effective?.Dpi ?? 300,
		color: colorModeLabels[$settings.Color ?? effective?.Color ?? 2] ?? 'Color',
		// A sample rather than a blank, so the chip shows what it is for and the style shows
		// what it does before anyone has scanned anything. Blank once Find name is off: the
		// preview then shows what the file would really be called, which is without it.
		name: $aiPrefs.findName ? styleName(SAMPLE_NAME, $exportPrefs.nameStyle) : ''
	});
	const templatePreview = $derived(
		$exportPrefs.nameTemplate.trim()
			? expandParts($exportPrefs.nameTemplate, new Date(), previewFacts)
			: []
	);

	/**
	 * Whole templates worth starting from, each shown as what it would produce. The examples
	 * are expanded with the same facts as the preview, so what the menu promises is what the
	 * field will show once it is picked.
	 */
	const templatePresets = $derived(
		[
			'{YEAR}-{MONTH}-{DAY}_{NAME}',
			'{NAME}',
			'{YEAR}-{MONTH}-{DAY}',
			'{YEAR}-{MONTH}-{DAY}_{HOUR}{MINUTE}',
			'{PRESET}-{COUNTER}'
		]
			// Nothing can fill `{NAME}` with Find name off, and `{NAME}` alone would offer a
			// template that expands to nothing at all.
			.filter((template) => $aiPrefs.findName || !template.includes('{NAME}'))
			.map((template) => ({
				template,
				example: expandTemplate(template, new Date(), previewFacts)
			}))
	);
	const templateUnknown = $derived(unknownTokens($exportPrefs.nameTemplate));

	const exportRoots = $derived(effective?.ExportRoots ?? []);
	/**
	 * Mirrors the server's containment rule closely enough to warn early. The server is
	 * still the authority — it resolves symlinks, which the browser cannot see.
	 */
	const outsideRoots = $derived.by(() => {
		const typed = $exportPrefs.directory.trim().replace(/\/+$/, '');
		if (typed === '' || exportRoots.length === 0) return false;
		return !exportRoots.some((root) => {
			const r = root.replace(/\/+$/, '');
			return typed === r || typed.startsWith(r + '/');
		});
	});

	function savePreset() {
		const name = draftName.trim();
		if (!name) return;
		const preset: Preset = {
			id: `${Date.now().toString(36)}-${name.toLowerCase().replace(/[^a-z0-9]+/g, '-')}`,
			name,
			icon: draftIcon,
			settings: { ...$settings }
		};
		presets.set([...$presets, preset]);
		drafting = false;
		draftName = '';
	}

	function updatePreset() {
		if (!chosen) return;
		presets.set(
			$presets.map((p) => (p.id === chosen.id ? { ...p, settings: { ...$settings } } : p))
		);
	}

	function deletePreset(id: string) {
		presets.set($presets.filter((p) => p.id !== id));
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

<div
	class="fixed inset-x-0 bottom-0 z-41 max-h-[92%] animate-pop-in overflow-y-auto rounded-t-[28px] bg-surface px-[22px] pt-[22px] pb-6 shadow-modal sm:inset-x-auto sm:top-1/2 sm:bottom-auto sm:left-1/2 sm:max-h-[calc(100%-32px)] sm:w-[min(1180px,calc(100%-40px))] sm:-translate-x-1/2 sm:-translate-y-1/2 sm:rounded-[26px] sm:px-[34px] sm:py-[30px]"
	role="dialog"
	aria-modal="true"
	aria-label="Scan settings"
>
	<span class="mx-auto mb-3 block h-1 w-10 rounded-full bg-handle sm:hidden" aria-hidden="true"
	></span>

	<div
		class="grid items-start gap-x-7 gap-y-[18px] sm:grid-cols-[repeat(auto-fit,minmax(320px,1fr))]"
	>
		<div class="col-span-full flex items-center gap-2">
			<h1 class="text-[20px] font-extrabold tracking-[-0.01em] sm:text-[26px]">Scan settings</h1>
			<div class="flex-1"></div>
			<button
				type="button"
				class="h-8 rounded-full px-3 text-[13px] font-bold text-accent transition-colors hover:bg-accent-wash"
				onclick={() => settings.reset()}
				data-testid="reset-settings"
			>
				Reset
			</button>
			<button
				type="button"
				class="flex h-9 w-9 items-center justify-center rounded-full bg-ctl transition-colors hover:bg-ctl-hover"
				onclick={close}
				aria-label="Close settings"
			>
				<Icon name="close" />
			</button>
		</div>

		<p class="col-span-full text-[13px] text-ink-muted">
			Stored in this browser only. Keys not listed here are fixed in
			<code class="font-mono">yass.yaml</code>.
		</p>

		<SettingsSection
			title="Scanner"
			hint="What the next scan asks the scanner for, kept in this browser. Only the keys scanner.ui_editable opens up can be set here."
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
				<p class="col-span-full text-ink-muted">Loading…</p>
			{:else if editable.size === 0}
				<p class="col-span-full text-ink-muted" data-testid="nothing-editable">
					Nothing is editable from the UI.
				</p>
			{:else}
				<div
					class="col-span-full flex flex-col gap-2.5 rounded-[18px] bg-app p-3.5"
					data-testid="presets"
				>
					<div class="flex items-center gap-2">
						<span class="text-[13px] font-bold text-ink-2">Presets</span>
						{#if !chosen}
							<span class="text-xs font-bold text-warning-ink">Unsaved changes</span>
						{/if}
					</div>

					<div class="flex flex-wrap gap-1.5">
						{#each $presets as preset (preset.id)}
							<button
								type="button"
								class="flex h-[38px] items-center gap-2 rounded-full px-3.5 text-[13px] font-bold transition-all duration-200 {chosen?.id ===
								preset.id
									? 'bg-accent text-white shadow-preset'
									: 'bg-surface text-accent-deep shadow-chip'}"
								aria-pressed={chosen?.id === preset.id}
								onclick={() => settings.set({ ...$settings, ...preset.settings })}
							>
								<Icon name={preset.icon} />
								{preset.name}
							</button>
						{/each}
					</div>

					{#if drafting}
						<div class="flex flex-col gap-2">
							<div class="flex flex-wrap gap-2">
								<input
									class="h-9 min-w-[160px] flex-1 rounded-[10px] border-[1.5px] border-accent bg-surface px-3 text-sm outline-none"
									type="text"
									placeholder="Preset name"
									bind:value={draftName}
									data-testid="preset-name"
								/>
								<button
									type="button"
									class="h-9 rounded-full bg-accent px-3 text-xs font-extrabold text-white transition-opacity disabled:pointer-events-none disabled:opacity-45"
									disabled={!draftName.trim()}
									onclick={savePreset}
									data-testid="preset-save"
								>
									Save
								</button>
								<button
									type="button"
									class="h-9 rounded-full px-3 text-xs font-bold text-ink-3 transition-colors hover:bg-ctl"
									onclick={() => (drafting = false)}
								>
									Cancel
								</button>
							</div>
							<div class="flex flex-wrap gap-1.5">
								{#each presetIcons as icon (icon)}
									<button
										type="button"
										class="flex h-9 w-9 items-center justify-center rounded-[10px] transition-transform duration-150 hover:scale-110 {draftIcon ===
										icon
											? 'bg-accent text-white shadow-swatch'
											: 'bg-surface text-ink-2 shadow-chip'}"
										aria-pressed={draftIcon === icon}
										aria-label={icon}
										onclick={() => (draftIcon = icon)}
									>
										<Icon name={icon} />
									</button>
								{/each}
							</div>
						</div>
					{:else}
						<div class="flex flex-wrap gap-1.5">
							{#if chosen}
								<button
									type="button"
									class="h-8 rounded-full bg-accent px-3 text-xs font-extrabold text-white transition-colors hover:bg-accent-hover"
									onclick={updatePreset}
								>
									Update “{chosen.name}”
								</button>
								<button
									type="button"
									class="h-8 rounded-full px-3 text-xs font-bold text-danger transition-colors hover:bg-danger-bg"
									onclick={() => deletePreset(chosen.id)}
								>
									Delete “{chosen.name}”
								</button>
							{/if}
							<button
								type="button"
								class="h-8 rounded-full bg-surface px-3 text-xs font-bold text-accent-deep shadow-chip transition-colors hover:bg-accent-50"
								onclick={() => (drafting = true)}
								data-testid="preset-new"
							>
								Save as new preset
							</button>
						</div>
					{/if}
				</div>

				<form
					class="col-span-full grid items-start gap-x-7 gap-y-[18px] sm:grid-cols-[repeat(auto-fit,minmax(320px,1fr))]"
					onsubmit={(e) => e.preventDefault()}
					data-testid="settings-form"
				>
					{#if editable.has('device') && devices && devices.length > 1}
						<!--
							Only worth a control when there is a choice: one scanner is not a decision, and
							the status pill in the header already says which one that is. A select rather
							than a segmented row — device names are long and there is no telling how many.
						-->
						<label class="flex flex-col gap-1.5" data-testid="device-picker">
							<span class="text-[13px] font-bold text-ink-2">
								Scanner
								<span class="font-semibold text-ink-muted">
									· which machine the next scan runs on</span
								>
							</span>
							<select
								class="h-11 rounded-[12px] bg-ctl px-3 text-sm outline-none focus:ring-2 focus:ring-accent"
								value={overrides.Device ?? ''}
								onchange={(e) =>
									settings.set({
										...$settings,
										Device: e.currentTarget.value === '' ? undefined : e.currentTarget.value
									})}
								data-testid="device-select"
							>
								<option value="">Whatever the server picks</option>
								{#each devices as device (device.Id)}
									<option value={device.Id}>{device.Name}</option>
								{/each}
							</select>
						</label>
					{/if}
					{#if editable.has('source')}
						<Segmented
							name="source"
							label="Source"
							options={withDefault(sources)}
							value={asSegment(overrides.Source)}
							onchange={(v) => setNumber('Source', v)}
						/>
					{/if}
					{#if editable.has('color')}
						<Segmented
							name="color"
							label="Colour mode"
							options={withDefault(colors)}
							value={asSegment(overrides.Color)}
							onchange={(v) => setNumber('Color', v)}
						/>
					{/if}
					{#if editable.has('dpi')}
						<Segmented
							name="dpi"
							label="Resolution"
							hint="dpi"
							options={withDefault(dpis)}
							value={asSegment(overrides.Dpi)}
							onchange={(v) => setNumber('Dpi', v)}
						/>
					{/if}
					{#if editable.has('page_size')}
						<Segmented
							name="page_size"
							label="Paper size"
							options={withDefault(pageSizes)}
							value={asSegment(overrides.PageSize)}
							onchange={(v) => setNumber('PageSize', v)}
						/>
					{/if}

					{#if editable.has('blank_page_detection') || editable.has('auto_deskew')}
						<div class="rounded-[16px] bg-app px-3.5 py-1.5">
							{#if editable.has('blank_page_detection')}
								<Toggle
									name="blank_page_detection"
									label="Skip blank pages"
									hint="Pages that look empty start out deleted"
									checked={overrides.BlankPageDetection ?? effective.BlankPageDetection ?? false}
									onchange={(v) => settings.update('BlankPageDetection', v)}
								/>
							{/if}
							{#if editable.has('auto_deskew')}
								<Toggle
									name="auto_deskew"
									label="Auto-deskew"
									hint="Straighten pages that went through crooked"
									checked={overrides.AutoDeskew ?? effective.AutoDeskew ?? false}
									onchange={(v) => settings.update('AutoDeskew', v)}
								/>
							{/if}
						</div>
					{/if}

					{#each [['brightness', 'Brightness', 'Brightness'], ['contrast', 'Contrast', 'Contrast']] as const as [key, label, prop] (key)}
						{#if editable.has(key)}
							<label class="flex flex-col gap-1.5">
								<span class="flex justify-between text-[13px] font-bold text-ink-2">
									{label}
									<span class="text-ink-muted tabular-nums">
										{overrides[prop] ?? effective[prop] ?? 0}
									</span>
								</span>
								<input
									class="h-11 accent-accent"
									type="range"
									name={key}
									min="-100"
									max="100"
									step="5"
									value={overrides[prop] ?? effective[prop] ?? 0}
									oninput={(e) =>
										settings.update(prop, Number((e.currentTarget as HTMLInputElement).value))}
								/>
							</label>
						{/if}
					{/each}

					{#if editable.has('blank_page_threshold')}
						<label class="flex flex-col gap-1.5">
							<span class="text-[13px] font-bold text-ink-2">
								Blank threshold
								<span class="font-semibold text-ink-muted">
									· default {effective.BlankPageThreshold}
								</span>
							</span>
							<input
								class="h-11 rounded-[12px] bg-ctl px-3 font-mono text-sm"
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
					{/if}
				</form>

				<p class="col-span-full text-[11px] text-ink-faint">
					Editable keys: {[...editable].join(', ')} — set by
					<code class="font-mono">scanner.ui_editable</code>.
				</p>

				{#if changed}
					<p
						class="col-span-full rounded-[12px] bg-success-bg px-3 py-2.5 text-xs font-bold text-success-ink"
						data-testid="overrides-active"
					>
						Overrides active for the next scan.
					</p>
				{/if}
			{/if}
		</SettingsSection>

		<!--
			Outside the scanner section above, and for the same reason: where a document goes,
			what it is called and how it is written are this browser's business. A server that
			lets the UI change no scan setting still has to let you name what comes off it.
		-->
		<SettingsSection
			title="Export"
			hint="Where a saved document goes and what kind of file it is."
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

			<Toggle
				name="export-button"
				label="Export button"
				hint="One-click export to a fixed folder. When off, Save as… is the way out."
				checked={$exportPrefs.exportButton}
				onchange={(v) => exportPrefs.set({ ...$exportPrefs, exportButton: v })}
			/>

			<!-- Dimmed rather than hidden: it is still where Export will go once it is back on. -->
			<label
				class="col-span-full flex flex-col gap-1.5 transition-opacity duration-200 {$exportPrefs.exportButton
					? ''
					: 'opacity-45'}"
			>
				<span class="text-[13px] font-bold text-ink-2">Export folder</span>
				<span class="flex h-11 items-center gap-2 rounded-[12px] bg-ctl px-3">
					<span class="text-ink-faint"><Icon name="folder" /></span>
					<input
						class="min-w-0 flex-1 bg-transparent font-mono text-sm font-medium outline-none"
						type="text"
						placeholder={exportRoots[0] ?? 'Leave empty for the configured folder'}
						value={$exportPrefs.directory}
						oninput={(e) => exportPrefs.set({ ...$exportPrefs, directory: e.currentTarget.value })}
						data-testid="export-folder"
					/>
				</span>

				{#if outsideRoots}
					<span
						class="text-xs font-bold text-danger"
						role="alert"
						data-testid="export-folder-error"
					>
						Outside the allowed folders. The server will refuse this.
					</span>
				{/if}

				<!--
					The roots are shown, not just enforced: a path that is going to be rejected should
					be visibly wrong here rather than at the moment someone presses Save.
				-->
				{#if exportRoots.length > 0}
					<span class="flex flex-wrap items-center gap-1.5">
						<span class="text-[11px] text-ink-faint">Allowed:</span>
						{#each exportRoots as root (root)}
							<button
								type="button"
								class="flex h-7 items-center rounded-[8px] bg-accent-50 px-2 font-mono text-[11px] font-semibold text-accent-deep transition-colors hover:bg-accent-100"
								onclick={() => exportPrefs.set({ ...$exportPrefs, directory: root })}
							>
								{root}
							</button>
						{/each}
					</span>
				{/if}

				<!--
					Says what the field is for, not just what it will accept: pointing it at a consume
					folder is the whole reason Export exists, and nothing else in the UI says so.
				-->
				<span class="text-[11px] text-ink-faint">
					Point this at a document manager's consume folder — paperless-ngx and the like watch
					theirs and file whatever lands in it, so an exported scan needs no upload.
				</span>

				<span class="text-[11px] text-ink-faint">
					An absolute path on the scanner server, inside one of the folders above. Widen them with <code
						class="font-mono">output.allowed_roots</code
					>
					in
					<code class="font-mono">yass.yaml</code>.
				</span>
			</label>
		</SettingsSection>

		<!--
			Outside the scanner section above: these are preferences of this browser's, with
			nothing to do with scanner.ui_editable, and they have to be reachable even on a
			server that lets the UI change nothing.
		-->
		<SettingsSection
			title="Naming"
			hint="What a document is called, before and after anything reads the page."
			testid="naming-section"
		>
			<div class="col-span-full flex flex-col gap-1.5">
				<span class="text-[13px] font-bold text-ink-2">File name template</span>
				<TemplateEditor
					value={$exportPrefs.nameTemplate}
					tokens={offeredTokens($aiPrefs.findName)}
					preview={templatePreview}
					presets={templatePresets}
					unknown={templateUnknown}
					extension={fileExtensions[$exportPrefs.format]}
					onchange={(v) => exportPrefs.set({ ...$exportPrefs, nameTemplate: v })}
				/>
				{#if $aiPrefs.findName}
					<span class="text-[11px] text-ink-faint">
						<code class="font-mono">{'{NAME}'}</code> is what
						<strong class="font-bold">Find name</strong> came back with, dropped in wherever you put it.
						Without it, picking a suggestion replaces the whole name.
					</span>
				{:else if $exportPrefs.nameTemplate.includes('{NAME}')}
					<!-- The token is left in the template rather than rewritten out of it: it is theirs,
					     and turning Find name back on should find it where they left it. -->
					<span class="text-[11px] text-ink-faint" data-testid="name-token-inert">
						This template still has <code class="font-mono">{'{NAME}'}</code> in it. With
						<strong class="font-bold">Find name</strong> off nothing fills it, so it is dropped from the
						saved name along with the separator beside it.
					</span>
				{/if}
			</div>

			<!-- Shapes a found name and nothing else, so it keeps the toggle's company. -->
			{#if $aiPrefs.findName}
				<div class="col-span-full">
					<Segmented
						name="name-style"
						label="Found names"
						hint="how the words of a found name are joined"
						options={nameStyles.map((n) => ({ value: n.value, label: n.label }))}
						value={$exportPrefs.nameStyle}
						onchange={(v) => exportPrefs.set({ ...$exportPrefs, nameStyle: v as NameStyle })}
					/>
				</div>
			{/if}

			<div class="col-span-full rounded-[16px] bg-app px-3.5 py-1.5">
				<Toggle
					name="find_name"
					label="Find name"
					badge="Experimental"
					hint="Suggest a file name from the words on the page — may change or go away"
					checked={$aiPrefs.findName}
					onchange={(v) => aiPrefs.set({ ...$aiPrefs, findName: v })}
				/>
			</div>

			<!--
				One flow of fields that depend on each other — a provider, then what that provider
				needs — so it takes the width rather than being broken across columns.
			-->
			<div class="col-span-full flex flex-col gap-3">
				{#if !$aiPrefs.findName}
					<p class="text-[11px] text-ink-faint" data-testid="find-name-off">
						The button is hidden on the review screen, and no page is read: the server only reads
						one when that button asks it to.
					</p>
				{:else}
					<Segmented
						name="ai-provider"
						label="Who writes it"
						hint="the heading on the page always counts; a model is optional"
						options={providers.map((p) => ({ value: p.value, label: p.label }))}
						value={provider}
						onchange={(v) => aiPrefs.set({ ...$aiPrefs, provider: v as AiProvider })}
					/>

					<!-- What the server can do is half the story, and the half nobody can guess. -->
					<p
						class="rounded-[12px] bg-app px-3 py-2.5 text-xs text-ink-muted"
						data-testid="ocr-state"
					>
						{#if effective === null}
							Asking the server what it can read…
						{:else if effective.CanReadPageText}
							This server reads page text, so the text is all that is ever sent — the page image
							stays here.
						{:else}
							This server does not read page text: <code class="font-mono">naming.ocr</code> is off
							in
							<code class="font-mono">yass.yaml</code>, or
							<code class="font-mono">tesseract</code> is not installed. Find name has nothing to work
							from, so it is hidden on the review screen.
						{/if}
					</p>

					{#if provider === 'off'}
						<p class="text-[11px] text-ink-faint">
							Find name still offers the heading it reads off the page and your file name template.
						</p>
					{:else if isInBrowser(provider)}
						{#if provider === 'webllm' && !canRunLocally}
							<p class="text-xs font-bold text-danger" role="alert" data-testid="no-webgpu">
								This browser has no WebGPU, so it cannot run a model on the graphics card. Pick
								In-browser (CPU) instead: it runs on the processor, needs no key either, and is only
								slower.
							</p>
						{:else}
							<label class="flex flex-col gap-1.5">
								<span class="text-[13px] font-bold text-ink-2">Model</span>
								<select
									class="h-11 rounded-[12px] bg-ctl px-3 font-mono text-sm outline-none focus:ring-2 focus:ring-accent"
									value={isPasted ? '' : localModel}
									onchange={(e) => setModel(e.currentTarget.value)}
									data-testid="ai-local-model"
								>
									<option value="">Choose a model…</option>
									{#each localModels as model (model.id)}
										<option value={model.id}>{model.label} · {model.sizeMb} MB</option>
									{/each}
								</select>
								<span class="text-[11px] text-ink-faint">
									Downloaded once from the internet, then kept in this browser and used offline.
									Every browser you use YASS from downloads its own copy. A model here cannot see
									the page, so it needs a server that reads page text.
									{#if provider === 'wllama'}
										It decodes on the processor and holds its weights in RAM, so budget roughly the
										download size again while it is answering.
									{/if}
								</span>
							</label>

							{#if provider === 'wllama'}
								<label class="flex flex-col gap-1.5">
									<span class="text-[13px] font-bold text-ink-2">Or a GGUF file of your own</span>
									<input
										class="h-11 rounded-[12px] bg-ctl px-3 font-mono text-sm outline-none focus:ring-2 focus:ring-accent"
										type="url"
										autocomplete="off"
										spellcheck="false"
										placeholder="https://huggingface.co/…/model.gguf"
										value={isPasted ? localModel : ''}
										onchange={(e) => setModel(e.currentTarget.value.trim())}
										data-testid="ai-gguf-url"
									/>
									<span class="text-[11px] text-ink-faint">
										Any instruct-tuned GGUF small enough for this machine. The host has to allow the
										request from a web page, which Hugging Face does.
									</span>
								</label>
							{/if}

							<div class="flex flex-col gap-1.5">
								<span class="flex items-center gap-2">
									<button
										type="button"
										class="h-10 rounded-[12px] bg-accent-50 px-3.5 text-[13px] font-bold text-accent transition-colors hover:bg-accent-100 disabled:pointer-events-none disabled:opacity-45"
										disabled={downloading !== null || !localModel}
										onclick={download}
										data-testid="ai-download"
									>
										{downloading === null ? 'Download model' : `Downloading… ${downloading}%`}
									</button>
									<button
										type="button"
										class="h-10 rounded-[12px] bg-accent-50 px-3.5 text-[13px] font-bold text-accent transition-colors hover:bg-accent-100 disabled:pointer-events-none disabled:opacity-45"
										disabled={testing || downloading !== null || !localModel}
										onclick={test}
										data-testid="ai-test"
									>
										{testing ? 'Asking…' : 'Test'}
									</button>
									<span class="text-[11px] text-ink-faint">
										Hundreds of megabytes, once. Better here than on a scan.
									</span>
								</span>
								{#if downloading !== null}
									<!-- Hundreds of megabytes deserves a bar, not a spinner: this is the one
								     wait in the app where knowing how much is left changes what you do. -->
									<div class="flex flex-col gap-1" data-testid="ai-download-progress">
										<ProgressBar percent={downloading} label="Model download" />
										<span class="text-[11px] text-ink-faint">
											{localLabel || 'The model'} — this browser keeps it, so this happens once.
										</span>
									</div>
								{/if}
								{#if testResult}
									<span
										class="text-xs font-bold {testResult.ok ? 'text-success-ink' : 'text-danger'}"
										role={testResult.ok ? 'status' : 'alert'}
										data-testid="ai-test-result"
									>
										{testResult.ok ? '✓ ' : ''}{testResult.message}
									</span>
								{/if}
							</div>
						{/if}
					{:else}
						<label class="flex flex-col gap-1.5">
							<span class="text-[13px] font-bold text-ink-2">API key</span>
							<input
								class="h-11 rounded-[12px] bg-ctl px-3 font-mono text-sm outline-none focus:ring-2 focus:ring-accent"
								type="password"
								autocomplete="off"
								spellcheck="false"
								placeholder={provider === 'anthropic' ? 'sk-ant-…' : 'sk-…'}
								value={key}
								oninput={(e) => setKey(e.currentTarget.value)}
								data-testid="ai-key"
							/>
							<span class="text-[11px] text-ink-faint">
								Kept in this browser and sent straight to the model, never to the scanner server.
								Anything running on this page can read it, and it stays until you clear it — think
								twice on a browser other people use.
							</span>
						</label>

						{#if provider === 'openai'}
							<label class="flex flex-col gap-1.5">
								<span class="text-[13px] font-bold text-ink-2">Base URL</span>
								<input
									class="h-11 rounded-[12px] bg-ctl px-3 font-mono text-sm outline-none focus:ring-2 focus:ring-accent"
									type="text"
									spellcheck="false"
									placeholder="https://api.openai.com/v1"
									value={$aiPrefs.baseUrl}
									oninput={(e) => aiPrefs.set({ ...$aiPrefs, baseUrl: e.currentTarget.value })}
									data-testid="ai-base-url"
								/>
								<span class="flex flex-wrap gap-1.5">
									{#each knownBaseUrls as known (known.url)}
										<button
											type="button"
											class="rounded-full bg-accent-50 px-2.5 py-1 font-mono text-[11px] font-bold text-accent transition-colors hover:bg-accent-100"
											onclick={() => aiPrefs.set({ ...$aiPrefs, baseUrl: known.url })}
										>
											{known.label}
										</button>
									{/each}
								</span>
								<span class="text-[11px] text-ink-faint">
									Anything that speaks the OpenAI chat shape. One running on your own machine has to
									allow requests from this page — Ollama needs
									<code class="font-mono">OLLAMA_ORIGINS</code>.
								</span>
							</label>
						{/if}

						<label class="flex flex-col gap-1.5">
							<span class="text-[13px] font-bold text-ink-2">Model</span>
							<input
								class="h-11 rounded-[12px] bg-ctl px-3 font-mono text-sm outline-none focus:ring-2 focus:ring-accent"
								type="text"
								spellcheck="false"
								placeholder={chosenModel($aiPrefs)}
								value={$aiPrefs.models[provider] ?? ''}
								oninput={(e) => setModel(e.currentTarget.value)}
								data-testid="ai-model"
							/>
						</label>

						<div class="flex flex-col gap-1.5">
							<span class="flex items-center gap-2">
								<button
									type="button"
									class="h-10 rounded-[12px] bg-accent-50 px-3.5 text-[13px] font-bold text-accent transition-colors hover:bg-accent-100 disabled:pointer-events-none disabled:opacity-45"
									disabled={testing || (needsKey && !key.trim())}
									onclick={test}
									data-testid="ai-test"
								>
									{testing ? 'Asking…' : 'Test'}
								</button>
								<span class="text-[11px] text-ink-faint">
									Sends one short document to {chosenBaseUrl($aiPrefs)}. It costs whatever your
									provider charges.
								</span>
							</span>
							{#if testResult}
								<span
									class="text-xs font-bold {testResult.ok ? 'text-success-ink' : 'text-danger'}"
									role={testResult.ok ? 'status' : 'alert'}
									data-testid="ai-test-result"
								>
									{testResult.ok ? '✓ ' : ''}{testResult.message}
								</span>
							{/if}
						</div>
					{/if}
				{/if}
			</div>
		</SettingsSection>

		<!--
			The one preference the review screen keeps, and it had nowhere to be said out loud:
			the overview's own zoom buttons set it, which you only find by opening the overview.
		-->
		<SettingsSection
			title="Editing"
			hint="How the review screen behaves while you go through the pages."
			testid="editing-section"
		>
			<Segmented
				name="overview-tile"
				label="Page size in the all-pages view"
				hint="Fit puts the whole scan on one screen; the zoom buttons in that view set the rest"
				options={tileSizes}
				value={String($reviewPrefs.overviewTile)}
				onchange={(v) =>
					reviewPrefs.set({ ...$reviewPrefs, overviewTile: v === 'fit' ? 'fit' : Number(v) })}
			/>
		</SettingsSection>

		<button
			type="button"
			class="col-span-full h-12 w-full justify-self-end rounded-[14px] bg-linear-135 from-accent-light to-accent-grad2 text-[15px] font-extrabold text-white shadow-done transition-shadow duration-200 hover:shadow-export-hover sm:w-[220px]"
			onclick={close}
		>
			Done
		</button>
	</div>
</div>
