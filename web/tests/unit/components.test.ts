import { describe, expect, it, vi } from 'vitest';
import { createRawSnippet } from 'svelte';
import { render, screen, fireEvent, waitFor } from '@testing-library/svelte';
import StatusPill from '$lib/components/StatusPill.svelte';
import Toolbar from '$lib/components/Toolbar.svelte';
import ExportBar from '$lib/components/ExportBar.svelte';
import PageThumb from '$lib/components/PageThumb.svelte';
import ScanStage from '$lib/components/ScanStage.svelte';
import SettingsChips from '$lib/components/SettingsChips.svelte';
import SortMenu from '$lib/components/SortMenu.svelte';
import AppFooter from '$lib/components/AppFooter.svelte';
import Toggle from '$lib/components/Toggle.svelte';
import PanelSection from '$lib/components/PanelSection.svelte';
import TemplateEditor from '$lib/components/TemplateEditor.svelte';
import { ScannerState } from '$lib/hub/generated/Yass.Core.Scanning';
import {
	ColorMode,
	PageFilter,
	PageSize,
	ScanSource,
	SessionState
} from '$lib/hub/generated/Yass.Core.Domain';
import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
import { page, session as build } from './fixtures';

const session = (state = SessionState.Review, deleted = 0): SessionDto =>
	build('s1', {
		State: state,
		Pages: [0, 1, 2].map((Index) => page(Index, { Deleted: Index < deleted }))
	});

describe('StatusPill', () => {
	it('shows scanner readiness when connected and offline otherwise', () => {
		const { rerender } = render(StatusPill, {
			status: { State: ScannerState.Ready, DeviceName: 'ADF', CheckedAt: '' },
			connection: 'connected'
		});
		expect(screen.getByRole('status')).toHaveTextContent(/Ready/);
		rerender({ status: null, connection: 'reconnecting' });
		expect(screen.getByRole('status')).toHaveTextContent(/Offline/);
	});
});

describe('ScanStage', () => {
	const stage = (
		state: ScannerState | null,
		connection: 'connected' | 'connecting' | 'reconnecting' = 'connected',
		Message?: string
	) =>
		render(ScanStage, {
			status: state === null ? null : { State: state, DeviceName: 'ADF', CheckedAt: '', Message },
			connection,
			options: null,
			onscan: vi.fn(),
			onsettings: vi.fn()
		});

	it('says nothing when the scanner is ready: the button being pressable is the message', () => {
		stage(ScannerState.Ready);
		expect(screen.getByTestId('scan-button')).toBeEnabled();
		expect(screen.queryByTestId('scanner-trouble')).toBeNull();
		expect(screen.getByTestId('scan-button-wrap')).not.toHaveAttribute('title');
		expect(screen.queryByText(/ready/i)).toBeNull();
	});

	it('greys the button out and says why, on hover and on screen', () => {
		const { unmount } = stage(ScannerState.NoDevice);
		expect(screen.getByTestId('scan-button')).toBeDisabled();
		expect(screen.getByTestId('scan-button-wrap')).toHaveAttribute('title', 'No scanner found');
		expect(screen.getByTestId('scanner-trouble')).toHaveTextContent('No scanner found');
		unmount();

		// What the scanner itself said wins over our own guess at the reason.
		stage(ScannerState.Error, 'connected', 'Cover open');
		expect(screen.getByTestId('scanner-trouble')).toHaveTextContent('Cover open');
		expect(screen.getByTestId('scan-button-wrap')).toHaveAttribute(
			'title',
			'The scanner reported a problem'
		);
	});

	it('blames the connection, not the scanner, when there is none', () => {
		stage(null, 'connecting');
		expect(screen.getByTestId('scan-button')).toBeDisabled();
		expect(screen.getByTestId('scanner-trouble')).toHaveTextContent(/Connecting/);
	});
});

describe('Toolbar', () => {
	it('emits edits, and says nothing about a selection it does not have', async () => {
		const onrotate = vi.fn();
		const onfilter = vi.fn();
		const onclear = vi.fn();
		render(Toolbar, {
			selectedCount: 0,
			total: 4,
			anyDeleted: false,
			onrotate,
			ondelete: vi.fn(),
			onrestore: vi.fn(),
			onfilter,
			onclear
		});
		// No scope line and no All: this toolbar edits the page it sits under, full stop.
		expect(screen.queryByTestId('toolbar-scope')).toBeNull();
		expect(screen.queryByTestId('toolbar-select-all')).toBeNull();
		await fireEvent.click(screen.getByRole('button', { name: 'Rotate right' }));
		expect(onrotate).toHaveBeenCalledWith(90);
		// Nothing is deleted, so there is nothing to restore and no button offering to.
		expect(screen.queryByTestId('restore-button')).toBeNull();
		await fireEvent.click(screen.getByTestId('filter-button'));
		await fireEvent.click(screen.getByRole('menuitem', { name: 'Black & white' }));
		expect(onfilter).toHaveBeenCalledWith(PageFilter.Bw);
	});

	it('offers expand only when asked to, and keeps it on while edits are off', async () => {
		const onexpand = vi.fn();
		const props = {
			selectedCount: 0,
			total: 3,
			anyDeleted: false,
			onrotate: vi.fn(),
			ondelete: vi.fn(),
			onrestore: vi.fn(),
			onfilter: vi.fn(),
			onclear: vi.fn()
		};
		const { rerender } = render(Toolbar, props);
		expect(screen.queryByTestId('expand-button')).toBeNull();

		// Disabled here means "not editable", e.g. mid-scan. The edits go, because none of
		// them can be made; looking at a page is not an edit, so Expand stays.
		await rerender({ ...props, onexpand, disabled: true });
		expect(screen.getByTestId('expand-button')).toBeEnabled();
		expect(screen.queryByRole('button', { name: 'Rotate right' })).toBeNull();
		expect(screen.queryByTestId('filter-button')).toBeNull();
		expect(screen.queryByTestId('delete-button')).toBeNull();
		await fireEvent.click(screen.getByTestId('expand-button'));
		expect(onexpand).toHaveBeenCalled();

		// Nothing scanned yet: there is no page to open, so Expand goes too.
		await rerender({ ...props, onexpand, total: 0 });
		expect(screen.queryByTestId('expand-button')).toBeNull();
	});

	it('shows Crop and Rescan for one page, and drops them for many or mid-scan', async () => {
		const props = {
			selectedCount: 0,
			total: 3,
			anyDeleted: false,
			oncrop: vi.fn(),
			onrescan: vi.fn(),
			onrotate: vi.fn(),
			ondelete: vi.fn(),
			onrestore: vi.fn(),
			onfilter: vi.fn(),
			onclear: vi.fn()
		};

		// Nothing ticked is the page on the stage — one page, so both are on offer.
		const { rerender } = render(Toolbar, props);
		expect(screen.getByTestId('crop-button')).toBeInTheDocument();
		expect(screen.getByTestId('rescan-button')).toBeInTheDocument();

		await rerender({ ...props, selectedCount: 1 });
		expect(screen.getByTestId('crop-button')).toBeInTheDocument();

		// Neither means anything for a set of pages, nor while the feeder is still going.
		await rerender({ ...props, selectedCount: 2 });
		expect(screen.queryByTestId('crop-button')).toBeNull();
		expect(screen.queryByTestId('rescan-button')).toBeNull();

		await rerender({ ...props, scanning: true });
		expect(screen.queryByTestId('crop-button')).toBeNull();
		expect(screen.queryByTestId('rescan-button')).toBeNull();
	});

	it('trades Delete for Restore according to what the pages already are', async () => {
		const props = {
			selectedCount: 0,
			total: 3,
			anyDeleted: false,
			anyActive: true,
			onrotate: vi.fn(),
			ondelete: vi.fn(),
			onrestore: vi.fn(),
			onfilter: vi.fn(),
			onclear: vi.fn()
		};

		// A page that is still there can be deleted, and there is nothing to put back.
		const { rerender } = render(Toolbar, props);
		expect(screen.getByTestId('delete-button')).toBeInTheDocument();
		expect(screen.queryByTestId('restore-button')).toBeNull();

		// Deleted, and the offer is the other way round — no second Delete for a page that
		// has already had one.
		await rerender({ ...props, anyDeleted: true, anyActive: false });
		expect(screen.getByTestId('restore-button')).toBeInTheDocument();
		expect(screen.queryByTestId('delete-button')).toBeNull();

		// Some of each: both mean something, so both are there.
		await rerender({ ...props, anyDeleted: true, anyActive: true });
		expect(screen.getByTestId('restore-button')).toBeInTheDocument();
		expect(screen.getByTestId('delete-button')).toBeInTheDocument();
	});

	it('acts on the page on screen until asked for the whole scan, where it is scoped', async () => {
		const onselectall = vi.fn();
		const props = {
			scoped: true,
			selectedCount: 0,
			total: 4,
			anyDeleted: false,
			onrotate: vi.fn(),
			ondelete: vi.fn(),
			onrestore: vi.fn(),
			onfilter: vi.fn(),
			onclear: vi.fn(),
			onselectall
		};
		const { rerender } = render(Toolbar, props);

		expect(screen.getByTestId('toolbar-scope')).toHaveTextContent(/this page/);
		await fireEvent.click(screen.getByTestId('toolbar-select-all'));
		expect(onselectall).toHaveBeenCalled();

		// Ticked pages are their own answer to "which ones", so Clear takes its place.
		await rerender({ ...props, selectedCount: 2 });
		expect(screen.queryByTestId('toolbar-select-all')).toBeNull();
		expect(screen.getByRole('button', { name: 'Clear' })).toBeVisible();

		// One page is the whole scan already; there is nothing for All to add.
		await rerender({ ...props, selectedCount: 0, total: 1 });
		expect(screen.queryByTestId('toolbar-select-all')).toBeNull();
	});

	it('shows the selection count and a clear button', async () => {
		const onclear = vi.fn();
		render(Toolbar, {
			scoped: true,
			selectedCount: 2,
			total: 4,
			anyDeleted: true,
			onrotate: vi.fn(),
			ondelete: vi.fn(),
			onrestore: vi.fn(),
			onfilter: vi.fn(),
			onclear
		});
		expect(screen.getByTestId('toolbar-scope')).toHaveTextContent(/2 selected/);
		await fireEvent.click(screen.getByRole('button', { name: 'Clear' }));
		expect(onclear).toHaveBeenCalled();
	});
});

describe('ExportBar', () => {
	it('exports to the configured folder and names it under the button', async () => {
		const onsave = vi.fn();
		render(ExportBar, {
			session: session(),
			allowDownload: true,
			exportDirectory: '/data/consume',
			onsave,
			ondownload: vi.fn()
		});
		expect(screen.getByTestId('export-path')).toHaveTextContent(/\/data\/consume/);
		expect(screen.getByTestId('autosave-strip')).toHaveTextContent(/Auto-saved to past scans/);
		await fireEvent.click(screen.getByTestId('save-button'));
		expect(onsave).toHaveBeenCalled();
	});

	it('drops the auto-save strip once the save card is saying the same thing', async () => {
		const props = {
			session: session(),
			allowDownload: true,
			onsave: vi.fn(),
			ondownload: vi.fn()
		};
		const { rerender } = render(ExportBar, props);
		expect(screen.getByTestId('autosave-strip')).toBeInTheDocument();

		// Saved: the card above is already green and already names the file.
		await rerender({ ...props, savedPath: '/data/consume/scan.pdf' });
		expect(screen.getByTestId('saved-message')).toBeInTheDocument();
		expect(screen.queryByTestId('autosave-strip')).toBeNull();
	});

	it('says what the one download button will do, and asks for a place when it can', async () => {
		const ondownload = vi.fn();
		const { rerender } = render(ExportBar, {
			session: session(),
			allowDownload: true,
			onsave: vi.fn(),
			ondownload
		});
		expect(screen.getByTestId('download-button')).toHaveTextContent('Download');

		await rerender({
			session: session(),
			allowDownload: true,
			picker: true,
			onsave: vi.fn(),
			ondownload
		});
		expect(screen.getByTestId('download-button')).toHaveTextContent('Save as…');
		await fireEvent.click(screen.getByTestId('download-button'));
		expect(ondownload).toHaveBeenCalled();
	});

	it('promotes the download button when the export button is off, and keeps one either way', () => {
		const { rerender } = render(ExportBar, {
			session: session(),
			allowDownload: true,
			exportEnabled: false,
			exportDirectory: '/data/consume',
			onsave: vi.fn(),
			ondownload: vi.fn()
		});
		expect(screen.queryByTestId('save-button')).toBeNull();
		expect(screen.queryByTestId('export-path')).toBeNull();
		expect(screen.getByTestId('download-button')).toBeInTheDocument();

		// Nothing left to promote: with downloads off, Export comes back whatever the preference says.
		rerender({
			session: session(),
			allowDownload: false,
			exportEnabled: false,
			onsave: vi.fn(),
			ondownload: vi.fn()
		});
		expect(screen.getByTestId('save-button')).toBeInTheDocument();
		expect(screen.queryByTestId('download-button')).toBeNull();
	});

	it('disables export while scanning, with everything deleted, or when downloads are off', () => {
		const { rerender } = render(ExportBar, {
			session: session(SessionState.Scanning),
			allowDownload: false,
			onsave: vi.fn(),
			ondownload: vi.fn()
		});
		expect(screen.getByTestId('save-button')).toBeDisabled();
		expect(screen.queryByTestId('download-button')).toBeNull();
		rerender({
			session: session(SessionState.Review, 3),
			allowDownload: true,
			onsave: vi.fn(),
			ondownload: vi.fn()
		});
		expect(screen.getByTestId('save-button')).toBeDisabled();
		expect(screen.getByTestId('download-button')).toBeDisabled();
	});

	it('shows progress while exporting and the saved message afterwards', () => {
		const { rerender } = render(ExportBar, {
			session: session(SessionState.Exporting),
			progress: 40,
			onsave: vi.fn(),
			ondownload: vi.fn()
		});
		expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '40');
		rerender({
			session: session(SessionState.Done),
			savedPath: '/data/consume/x.pdf',
			onsave: vi.fn(),
			ondownload: vi.fn()
		});
		expect(screen.getByTestId('saved-message')).toBeInTheDocument();
	});
});

describe('PageThumb', () => {
	it('shows on tap, selects on the checkbox, opens on the expand button, and badges blank/deleted pages', async () => {
		const onshow = vi.fn();
		const ontoggle = vi.fn();
		const onopen = vi.fn();
		const { container } = render(PageThumb, {
			sessionId: 's1',
			page: page(2, { Deleted: true, IsBlank: true, Hash: 'abc' }),
			onshow,
			ontoggle,
			onopen
		});
		expect(screen.getByTestId('blank-badge')).toBeInTheDocument();
		expect(screen.getByTestId('page-thumb')).toHaveAttribute('data-deleted', 'true');
		expect(container.querySelector('img')).toHaveAttribute(
			'src',
			expect.stringContaining('/img/s1/2/thumb?h=abc')
		);
		// Tapping the page only moves the stage: selection is the checkbox's job alone.
		await fireEvent.click(screen.getByRole('button', { name: /^Show page 3/ }));
		expect(onshow).toHaveBeenCalledWith(2);
		expect(ontoggle).not.toHaveBeenCalled();
		await fireEvent.click(screen.getByRole('button', { name: 'Select page 3' }));
		expect(ontoggle).toHaveBeenCalledWith(2);
		await fireEvent.click(screen.getByRole('button', { name: 'Open page 3' }));
		expect(onopen).toHaveBeenCalledWith(2);
	});

	it('leaves out the expand button where the toolbar offers that instead', () => {
		render(PageThumb, {
			sessionId: 's1',
			page: page(0),
			onshow: vi.fn(),
			ontoggle: vi.fn()
		});
		expect(screen.queryByRole('button', { name: 'Open page 1' })).toBeNull();
	});

	it('shows a spinner until the thumbnail exists', () => {
		const { container } = render(PageThumb, {
			sessionId: 's1',
			page: page(0, { Hash: 'h', ThumbnailReady: false }),
			onshow: vi.fn(),
			ontoggle: vi.fn(),
			onopen: vi.fn()
		});
		expect(container.querySelector('img')).toBeNull();
	});
});

describe('SettingsChips', () => {
	const options = {
		Source: ScanSource.Feeder,
		Dpi: 300,
		Color: ColorMode.Color,
		PageSize: PageSize.A4,
		BlankPageDetection: true
	};

	it('leads with the export destination and ends with the format', () => {
		render(SettingsChips, {
			options,
			destination: '/data/consume',
			format: 'PDF',
			onclick: vi.fn()
		});
		const chips = [...screen.getByTestId('settings-chips').children].map((c) =>
			c.textContent?.trim()
		);
		expect(chips[0]).toBe('→ /data/consume');
		expect(chips.at(-1)).toBe('PDF');
		expect(chips).toContain('300 dpi');
	});

	it('leaves the destination out when there is none to show', () => {
		render(SettingsChips, { options, onclick: vi.fn() });
		const chips = [...screen.getByTestId('settings-chips').children].map((c) =>
			c.textContent?.trim()
		);
		expect(chips.some((c) => c?.startsWith('→'))).toBe(false);
	});
});

describe('SortMenu', () => {
	it('names the order it is sorting by, not just "Sort"', async () => {
		const onchange = vi.fn();
		const { rerender } = render(SortMenu, { order: 'newest', onchange });
		expect(screen.getByTestId('sort-button')).toHaveTextContent(/Newest/);
		await rerender({ order: 'name', onchange });
		expect(screen.getByTestId('sort-button')).toHaveTextContent(/Name/);
	});
});

describe('AppFooter', () => {
	it('links out to the repository, and marks the links external', () => {
		render(AppFooter);

		const github = screen.getByRole('link', { name: /GitHub/ });
		expect(github).toHaveAttribute('href', 'https://github.com/antonkesy/YAWSS');

		expect(screen.getByRole('link', { name: /Report an issue/ })).toHaveAttribute(
			'href',
			'https://github.com/antonkesy/YAWSS/issues/new'
		);

		// rel=external keeps SvelteKit's router out of the way, and is what lets these
		// pass svelte/no-navigation-without-resolve without loosening the rule.
		for (const link of screen.getAllByRole('link')) {
			expect(link.getAttribute('rel')).toContain('external');
			expect(link).toHaveAttribute('target', '_blank');
		}
	});
});

describe('Toggle', () => {
	it('wears a badge when given one, and goes without when not', () => {
		const { unmount } = render(Toggle, {
			name: 'find_name',
			label: 'Find name',
			hint: 'Suggest a file name from the words on the page',
			badge: 'Experimental',
			checked: false,
			onchange: vi.fn()
		});
		expect(screen.getByTestId('toggle-badge-find_name')).toHaveTextContent('Experimental');
		unmount();

		// A fresh render rather than a rerender: rerender merges props, so leaving `badge`
		// out of it would keep the one before rather than test the default.
		render(Toggle, {
			name: 'blank_pages',
			label: 'Blank pages',
			checked: true,
			onchange: vi.fn()
		});
		expect(screen.queryByTestId('toggle-badge-blank_pages')).toBeNull();
	});

	it('leaves the label at the front of the accessible name', () => {
		// The badge sits inside the label, so it is read out with it — which is the point,
		// a colour being no warning at all to anyone who cannot see it. It goes after the
		// label so that the name still starts with what the setting is called.
		render(Toggle, {
			name: 'find_name',
			label: 'Find name',
			badge: 'Experimental',
			checked: false,
			onchange: vi.fn()
		});

		expect(screen.getByRole('checkbox', { name: /^Find name/ })).toBeInTheDocument();
	});
});

describe('TemplateEditor', () => {
	const editor = (tokens: readonly string[]) => ({
		value: '{YEAR}-{MONTH}-{DAY}',
		tokens,
		preview: [{ text: '2026-09-24', name: false }],
		unknown: [],
		onchange: vi.fn()
	});

	it('offers a chip per token it is handed, and none it is not', async () => {
		const { rerender } = render(TemplateEditor, editor(['YEAR', 'MONTH', 'NAME']));
		expect(screen.getByRole('button', { name: '{NAME}' })).toBeInTheDocument();

		await rerender(editor(['YEAR', 'MONTH']));
		expect(screen.queryByRole('button', { name: '{NAME}' })).toBeNull();
		expect(screen.getByRole('button', { name: '{YEAR}' })).toBeInTheDocument();
	});

	it('ends every name in the extension the export format asks for', async () => {
		// The preview is a promise about the file that will be written, so a JPG export must
		// not be previewed as a PDF. Three places say it: the field, the preview, and each
		// ready-made template in the menu.
		const { rerender } = render(TemplateEditor, {
			...editor(['YEAR']),
			extension: '.jpg',
			presets: [{ template: '{YEAR}', example: '2026' }]
		});

		expect(screen.getByTestId('template-preview')).toHaveTextContent('2026-09-24.jpg');
		await fireEvent.click(screen.getByTestId('template-presets'));
		expect(screen.getByRole('menuitem')).toHaveTextContent('2026.jpg');

		await rerender({ ...editor(['YEAR']), extension: '.pdf' });
		expect(screen.getByTestId('template-preview')).toHaveTextContent('2026-09-24.pdf');
	});

	it('tints the found name only where the preview says there is one', async () => {
		const { rerender } = render(TemplateEditor, editor(['YEAR']));
		expect(screen.queryByTestId('template-preview-name')).toBeNull();

		await rerender({
			...editor(['YEAR', 'NAME']),
			preview: [
				{ text: '2026-09-24_', name: false },
				{ text: 'quarterly-report', name: true }
			]
		});
		expect(screen.getByTestId('template-preview-name')).toHaveTextContent('quarterly-report');
	});
});

describe('PanelSection', () => {
	const body = createRawSnippet(() => ({ render: () => '<p data-testid="body">inside</p>' }));

	it('says what it holds while it is shut, and opens on demand', async () => {
		const { rerender } = render(PanelSection, {
			title: 'Scan',
			summary: '2 of 3 pages',
			summaryTestid: 'page-count',
			testid: 'scan-section',
			children: body
		});

		const details = screen.getByTestId('scan-section') as HTMLDetailsElement;
		expect(details.open).toBe(false);
		// Shut is not gone: the summary is what you read without opening anything.
		expect(screen.getByTestId('page-count')).toHaveTextContent('2 of 3 pages');

		await rerender({
			title: 'Scan',
			summary: '2 of 3 pages',
			summaryTestid: 'page-count',
			testid: 'scan-section',
			open: true,
			children: body
		});
		expect((screen.getByTestId('scan-section') as HTMLDetailsElement).open).toBe(true);
		expect(screen.getByTestId('body')).toBeInTheDocument();
	});

	it('reports which way it was turned, so the choice can be remembered', async () => {
		const ontoggle = vi.fn();
		render(PanelSection, { title: 'More', testid: 'more-section', ontoggle, children: body });

		// The `open` property rather than a click on the summary: jsdom implements no summary
		// activation behaviour, so clicking one opens nothing and the test would pass by
		// asserting a toggle that never happened. jsdom does queue `toggle` off the attribute.
		const details = screen.getByTestId('more-section') as HTMLDetailsElement;
		details.open = true;
		await waitFor(() => expect(ontoggle).toHaveBeenLastCalledWith(true));

		details.open = false;
		await waitFor(() => expect(ontoggle).toHaveBeenLastCalledWith(false));
	});
});
