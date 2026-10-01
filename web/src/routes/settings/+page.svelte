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
	import TemplateEditor from '$lib/components/TemplateEditor.svelte';
	import { Icon, presetIcons, type PresetIconName } from '$lib/icons';
	import { activePreset, exportPrefs, presets, reviewPrefs, type Preset } from '$lib/presets';
	import { TILE_SIZES } from '$lib/review/overview';
	import { ExportFormat } from '$lib/hub/generated/Yass.Core.Hub';
	import { exportFormatLabels } from '$lib/format';
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
	const changed = $derived(Object.values(editableKeys).some((key) => overrides[key] !== undefined));

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
	const chosen = $derived(activePreset($presets, planned));
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
		drafting = false;
		draftName = '';
		draftShared = false;
	}

	function updatePreset() {
		if (!chosen) return;
		presets.set(
			$presets.map((p) => (p.id === chosen.id ? { ...p, settings: withoutDevice(planned) } : p))
		);
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
			Stored in this browser only. Anything left on Default comes from
			<code class="font-mono">yass.yaml</code>.
		</p>

		<SettingsSection
			title="Scanner"
			hint="What the next scan asks the scanner for, kept in this browser. Default is whatever yass.yaml says."
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
								<!-- The home screen only offers this one on that scanner, so this sheet — the
								     one place every preset is listed — has to say which. -->
								{#if preset.device}
									<span class="max-w-[110px] truncate text-[11px] font-semibold opacity-65"
										>{deviceLabel(preset.device)}</span
									>
								{/if}
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
						{#if chosen && (currentDevice || chosen.device)}
							<label class="flex flex-wrap items-center gap-2 text-xs font-semibold text-ink-2">
								<input
									type="checkbox"
									class="h-4 w-4 accent-accent"
									checked={!chosen.device}
									onchange={(e) => sharePreset(chosen.id, e.currentTarget.checked)}
									data-testid="preset-scope"
								/>
								“{chosen.name}” is available for all scanners
								<span class="font-normal text-ink-muted"
									>· otherwise only on {deviceLabel(chosen.device || currentDevice)}</span
								>
							</label>
						{/if}
					{/if}
				</div>

				<form
					class="col-span-full grid items-start gap-x-7 gap-y-[18px] sm:grid-cols-[repeat(auto-fit,minmax(320px,1fr))]"
					onsubmit={(e) => e.preventDefault()}
					data-testid="settings-form"
				>
					<Segmented
						name="source"
						label="Source"
						options={withDefault(sources)}
						value={asSegment(overrides.Source)}
						onchange={(v) => setNumber('Source', v)}
					/>
					<Segmented
						name="color"
						label="Colour mode"
						options={withDefault(colors)}
						value={asSegment(overrides.Color)}
						onchange={(v) => setNumber('Color', v)}
					/>
					<Segmented
						name="dpi"
						label="Resolution"
						hint="dpi"
						options={withDefault(dpis)}
						value={asSegment(overrides.Dpi)}
						onchange={(v) => setNumber('Dpi', v)}
					/>
					<Segmented
						name="page_size"
						label="Paper size"
						options={withDefault(pageSizes)}
						value={asSegment(overrides.PageSize)}
						onchange={(v) => setNumber('PageSize', v)}
					/>

					<div class="rounded-[16px] bg-app px-3.5 py-1.5">
						<Toggle
							name="blank_page_detection"
							label="Skip blank pages"
							hint="Pages that look empty start out deleted"
							checked={overrides.BlankPageDetection ?? effective.BlankPageDetection ?? false}
							onchange={(v) => settings.update('BlankPageDetection', v)}
						/>
						<Toggle
							name="auto_deskew"
							label="Auto-deskew"
							hint="Straighten pages that went through crooked"
							checked={overrides.AutoDeskew ?? effective.AutoDeskew ?? false}
							onchange={(v) => settings.update('AutoDeskew', v)}
						/>
					</div>

					{#each [['brightness', 'Brightness', 'Brightness'], ['contrast', 'Contrast', 'Contrast']] as const as [key, label, prop] (key)}
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
					{/each}

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
				</form>

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
			nothing to do with how the scanner is driven, and they have to stay reachable
			while that section is still loading or has failed.
		-->
		<SettingsSection
			title="Naming"
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
			hint="Written into every PDF's properties. The title is always the document's name."
			testid="metadata-section"
		>
			{#each [['author', 'Author', 'Who the documents belong to. Left empty, the PDF names nobody.'], ['subject', 'Subject', 'What they are about, if every scan from here is about the same thing.'], ['keywords', 'Keywords', 'Comma-separated, for whatever indexes them.']] as const as [key, label, hint] (key)}
				<label class="flex flex-col gap-1.5">
					<span class="text-[13px] font-bold text-ink-2">{label}</span>
					<input
						class="h-11 rounded-[12px] bg-ctl px-3 text-sm font-medium outline-none"
						type="text"
						name="metadata-{key}"
						autocomplete="off"
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
