<script lang="ts">
	import { autoAnimate } from '$lib/autoAnimate';
	import { getDesktop, getHub } from '$lib/hub/hub';
	import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
	import { ExportFormat, type SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
	import type { MessageKey } from '$lib/i18n/index.svelte';
	import { t } from '$lib/i18n/index.svelte';
	import { Icon } from '$lib/icons';
	import {
		activePageCount,
		colorLabel,
		documentName,
		exportFormatLabels,
		formatBytes,
		formatTime,
		pageSizeLabel,
		sourceLabel
	} from '$lib/format';
	import { exportPrefs, scannerPrefs } from '$lib/presets';
	import { exportBaseName, fileExtensions, savePicker } from '$lib/review/download';
	import type { Review } from '$lib/review/review.svelte';
	import { scannerName } from '$lib/scanners';
	import ExportBar from './ExportBar.svelte';
	import NameField from './NameField.svelte';
	import PdfProperties from './PdfProperties.svelte';
	import Segmented from './Segmented.svelte';

	let {
		session,
		review,
		mergeable,
		onmerge
	}: {
		session: SessionDto;
		review: Review;
		/** How many other scans have pages to add to this one. */
		mergeable: number;
		/** Opens the dialog that picks one of them. */
		onmerge: () => void;
	} = $props();

	const client = getHub();
	const desktop = getDesktop();
	const exportProgress = client.exportProgress;
	const serverFolder = client.serverFolder;
	const scannerStatus = client.scanner;

	const editable = $derived(
		session.State === SessionState.Review || session.State === SessionState.Done
	);
	/** Why the sidebar's buttons are greyed while the scan is not in review, for their tooltips. */
	const busyReason = $derived(
		session.State === SessionState.Scanning
			? t('review.waitScan')
			: session.State === SessionState.Exporting
				? t('review.waitSave')
				: t('review.notEditable')
	);

	/**
	 * What the field shows, and the same thing the list of past scans shows for this scan: a
	 * scan nobody has named reads as the template from settings, which is what it will be
	 * saved as.
	 */
	const displayName = $derived(
		documentName({ ...session, Name: review.storedName ?? undefined }, $exportPrefs.nameTemplate)
	);

	const formatOptions = $derived(
		[ExportFormat.Pdf, ExportFormat.Jpg, ExportFormat.Png].map((f) => ({
			value: String(f),
			label: exportFormatLabels[f]
		}))
	);
	/** The scanner the scan ran on, by the name this browser gives it. */
	const scannerLabel = $derived(
		session.OptionsUsed.Device
			? scannerName($scannerPrefs, session.OptionsUsed.Device)
			: ($scannerStatus?.DeviceName ?? '')
	);
	/** The settings the scan ran with, each on its own tile; one the scan did not record is left out. */
	const scanSettings = $derived.by((): [MessageKey, string][] => {
		const o = session.OptionsUsed;
		const all: [MessageKey, string][] = [
			['settings.source', sourceLabel(o.Source)],
			['settings.resolution', o.Dpi ? t('home.dpi', { n: o.Dpi }) : ''],
			['settings.colourMode', colorLabel(o.Color)],
			['settings.paperSize', pageSizeLabel(o.PageSize)]
		];
		return all.filter(([, value]) => value);
	});
	/**
	 * What the Scan section says while it is shut. Worded exactly as the line it replaces, which
	 * sat under the download button: it is a fact about the paper, not about the saving.
	 */
	const pageSummary = $derived(
		t('review.pageSummary', { active: activePageCount(session), n: session.Pages.length })
	);

	let confirmDiscard = $state(false);

	const ghost =
		'flex h-[42px] items-center gap-2.5 rounded-[12px] px-2.5 text-left text-sm font-semibold transition-colors hover:bg-accent-hover50 disabled:pointer-events-none disabled:opacity-40';
</script>

{#snippet heading(title: string, value = '', testid = '')}
	<div class="flex items-baseline gap-2">
		<span class="text-[13px] font-semibold text-ink-muted">{title}</span>
		{#if value}
			<span
				class="ml-auto min-w-0 truncate text-[13px] font-semibold text-ink-muted"
				data-testid={testid || undefined}>{value}</span
			>
		{/if}
	</div>
{/snippet}

{#snippet tile(label: string, value: string, wide = false)}
	<!-- Label over value, two to a row: the facts of a scan read at a glance in a narrow card. -->
	<div
		class="flex min-w-0 flex-col gap-0.5 rounded-[12px] bg-app px-2.5 py-2 {wide
			? 'col-span-2'
			: ''}"
	>
		<span class="truncate text-[11px] text-ink-muted">{label}</span>
		<span class="truncate text-[13px] font-bold tracking-[-0.01em] tabular-nums" title={value}
			>{value}</span
		>
	</div>
{/snippet}

<!--
	Width kept close to the filmstrip's: the stage sits between the two, so the page
	on screen is only centred while the columns either side of it match. Neither can
	be the other's exact width — thumbnails do not want 280px and this card does not
	want 180 — so they meet in the middle and the page lands near enough to centre.
-->
<aside
	class="order-3 flex w-full min-w-0 basis-full flex-col gap-3.5 rounded-[24px] bg-surface p-[22px] shadow-panel lg:min-h-0 lg:w-auto lg:basis-(--review-aside-w) lg:overflow-y-auto"
	{@attach autoAnimate}
>
	{@render heading(t('review.headingName'))}

	<NameField
		value={displayName}
		placeholder={exportBaseName(session, $exportPrefs.nameTemplate)}
		extension={fileExtensions[$exportPrefs.format]}
		onrename={(name) => review.rename(name)}
	/>

	{@render heading(t('review.headingSave'))}

	<ExportBar
		{session}
		progress={$exportProgress[session.Id] ?? null}
		busy={review.exporting}
		picker={desktop !== null || savePicker() !== null}
		ondownload={() => review.saveAs()}
		serverFolder={$exportPrefs.serverSave && $serverFolder ? $serverFolder : null}
		onserver={() => review.saveToServer()}
		onstop={() => review.stop()}
		stopping={review.stopping}
	/>

	<!-- What the file will be, together under the button that saves it. -->
	<div class="flex flex-col rounded-[16px] bg-app p-3" data-testid="file-options">
		<!-- Settings → Export holds the same choice; switching here switches it there. -->
		<Segmented
			name="review-format"
			label={t('settings.fileFormat')}
			hint={$exportPrefs.format === ExportFormat.Pdf
				? t('settings.perDocument')
				: t('settings.perPage')}
			options={formatOptions}
			value={String($exportPrefs.format)}
			onchange={(v) => exportPrefs.set({ ...$exportPrefs, format: Number(v) as ExportFormat })}
		/>

		<PdfProperties sessionId={session.Id} />
	</div>

	<!-- The facts about the scan, with the page count up on the heading beside them. -->
	<div class="flex flex-col gap-1.5" data-testid="scan-section">
		{@render heading(t('review.headingScan'), pageSummary, 'page-count')}
		<div class="mt-1 grid grid-cols-2 gap-1.5">
			{@render tile(t('review.scanned'), formatTime(session.CreatedAt))}
			<!-- The scanned pages on disk, not an estimate of the PDF the mockup guesses at. -->
			{@render tile(t('review.size'), formatBytes(session.SizeBytes))}
			{#if scannerLabel}
				{@render tile(t('review.scanner'), scannerLabel, true)}
			{/if}
			{#each scanSettings as [label, value] (label)}
				{@render tile(t(label), value)}
			{/each}
		</div>
	</div>

	<div class="flex flex-col gap-0.5" data-testid="more-section">
		{@render heading(t('review.headingMore'))}
		<!-- A greyed button says why on hover: rule of thumb, no dead control without a reason. -->
		<button
			type="button"
			class={ghost}
			onclick={() => review.scanMore()}
			disabled={!editable}
			title={editable ? undefined : busyReason}
			data-testid="scan-more-button"
		>
			<Icon name="plus" />
			{t('review.scanMorePages')}
		</button>
		<button
			type="button"
			class={ghost}
			onclick={onmerge}
			disabled={!editable || mergeable === 0 || review.mergeBusy}
			title={!editable ? busyReason : mergeable === 0 ? t('review.noMergeable') : undefined}
			data-testid="merge-button"
		>
			<Icon name="layers" />
			{t('review.addFromPastScan')}
		</button>
	</div>

	<!--
		Alone at the foot, because it throws the scan away and has no business sitting
		where a hand goes looking for Scan more. mt-auto puts it on the bottom edge
		while the panel has room to spare, and is simply inert once it has not.
	-->
	<div class="mt-auto border-t border-line pt-4 max-lg:mt-4">
		<button
			type="button"
			class="{ghost} w-full text-danger hover:bg-danger-bg"
			onclick={() => (confirmDiscard ? review.discard() : (confirmDiscard = true))}
			onblur={() => (confirmDiscard = false)}
			data-testid="discard-button"
		>
			<Icon name="trash" />
			{confirmDiscard ? t('review.reallyDiscard') : t('review.discardScan')}
		</button>
	</div>
</aside>
