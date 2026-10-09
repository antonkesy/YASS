import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import StatusPill from '$lib/components/StatusPill.svelte';
import Toolbar from '$lib/components/Toolbar.svelte';
import ExportBar from '$lib/components/ExportBar.svelte';
import PageThumb from '$lib/components/PageThumb.svelte';
import ScanStage from '$lib/components/ScanStage.svelte';
import SettingsChips from '$lib/components/SettingsChips.svelte';
import SortMenu from '$lib/components/SortMenu.svelte';
import TemplateEditor from '$lib/components/TemplateEditor.svelte';
import AdjustModal from '$lib/components/AdjustModal.svelte';
import StraightenModal from '$lib/components/StraightenModal.svelte';
import Preview from '$lib/components/Preview.svelte';
import { layer } from '$lib/keys.svelte';
import CropModal from '$lib/components/CropModal.svelte';
import PresetPills from '$lib/components/PresetPills.svelte';
import { ScannerState } from '$lib/hub/generated/Yass.Core.Scanning';
import {
	ColorMode,
	PageCleanup,
	PageFilter,
	PageSize,
	Rotation,
	ScanSource,
	SessionState
} from '$lib/hub/generated/Yass.Core.Domain';
import type { ScanOptionsDto, SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
import { presets } from '$lib/presets';
import { settings } from '$lib/settings';
import { get } from 'svelte/store';
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

	it('counts the pages of a running scan, one or many', () => {
		const busy = { State: ScannerState.Busy, DeviceName: 'ADF', CheckedAt: '' };
		const { rerender } = render(StatusPill, { status: busy, connection: 'connected', pages: 1 });
		expect(screen.getByRole('status')).toHaveTextContent('Scanning · 1 page');
		rerender({ status: busy, connection: 'connected', pages: 3 });
		expect(screen.getByRole('status')).toHaveTextContent('Scanning · 3 pages');
		rerender({ status: busy, connection: 'connected', pages: 0 });
		expect(screen.getByRole('status')).toHaveTextContent(/^Scanning$/);
	});

	it('opens a panel of scanner details on click', async () => {
		render(StatusPill, {
			status: {
				State: ScannerState.Ready,
				DeviceName: 'ADF',
				Message: 'Flatbed lid open',
				CheckedAt: new Date().toISOString()
			},
			connection: 'connected'
		});
		expect(screen.queryByRole('dialog')).toBeNull();
		await fireEvent.click(screen.getByTestId('status-pill'));
		const panel = screen.getByRole('dialog', { name: 'Scanner details' });
		expect(panel).toHaveTextContent('ADF');
		expect(panel).toHaveTextContent('Flatbed lid open');
		expect(panel).toHaveTextContent('Connected');
		await fireEvent.click(screen.getByRole('button', { name: 'Close scanner details' }));
		expect(screen.queryByRole('dialog')).toBeNull();
	});

	it('shows the details while hovered, without a click-catcher', async () => {
		render(StatusPill, {
			status: { State: ScannerState.Ready, DeviceName: 'ADF', CheckedAt: '' },
			connection: 'connected'
		});
		const wrapper = screen.getByTestId('status-pill').parentElement!;
		await fireEvent.mouseEnter(wrapper);
		expect(screen.getByRole('dialog', { name: 'Scanner details' })).toBeInTheDocument();
		expect(screen.queryByRole('button', { name: 'Close scanner details' })).toBeNull();
		await fireEvent.mouseLeave(wrapper);
		expect(screen.queryByRole('dialog')).toBeNull();
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

describe('ScanStage scanner picker', () => {
	const epson = { Id: 'epson:1', Name: 'Epson DS-530' };
	const canon = { Id: 'canon:2', Name: 'Canon LiDE 400' };

	const stage = (options: Partial<ScanOptionsDto> | null, devices = [epson, canon]) =>
		render(ScanStage, {
			status: { State: ScannerState.Ready, DeviceName: 'ADF', CheckedAt: '' },
			connection: 'connected' as const,
			options: options === null ? null : (options as ScanOptionsDto),
			devices,
			deviceName: 'Epson DS-530',
			onscan: vi.fn(),
			onsettings: vi.fn()
		});

	afterEach(() => {
		settings.reset();
		presets.reset();
	});

	const picked = () => screen.getByRole('radio', { checked: true }).getAttribute('data-device');

	it('lists what the server can see and lights the one the scan will use', () => {
		stage({ Device: 'canon:2' });
		expect(picked()).toBe('canon:2');
		expect(screen.getAllByRole('radio').map((o) => o.textContent?.trim())).toEqual([
			'Epson DS-530',
			'Canon LiDE 400'
		]);
	});

	it("lights the server's scanner when this browser has not picked one", () => {
		const { unmount } = stage({ Device: '' }, [epson, { Id: 'adf:3', Name: 'ADF' }]);
		expect(picked()).toBe('adf:3');
		unmount();

		// The server's name matches nothing listed: the first one stands in rather than none.
		stage({ Device: '' });
		expect(picked()).toBe('epson:1');
	});

	it('keeps a scanner the server has stopped listing visible rather than lighting another', () => {
		stage({ Device: 'gone:9' });
		expect(picked()).toBe('gone:9');
		expect(screen.getByRole('radio', { name: 'gone:9 (not found)' })).toBeChecked();
	});

	it("writes the pick into this browser's overrides", async () => {
		stage({ Device: '' });
		await fireEvent.click(screen.getByRole('radio', { name: 'Canon LiDE 400' }));
		expect(get(settings).Device).toBe('canon:2');
	});

	it('opens the details of the lit scanner on a press, and only of that one', async () => {
		stage({ Device: 'epson:1' });
		await fireEvent.click(screen.getByRole('radio', { name: 'Canon LiDE 400' }));
		expect(screen.queryByRole('dialog')).toBeNull();
		await fireEvent.click(screen.getByRole('radio', { name: 'Epson DS-530' }));
		expect(screen.getByRole('dialog', { name: 'Scanner details' })).toHaveTextContent(
			'Epson DS-530'
		);
	});

	it("shows the lit scanner's details while hovered, without a click-catcher", async () => {
		stage({ Device: 'epson:1' });
		const pill = screen.getByRole('radio', { name: 'Epson DS-530' });
		await fireEvent.mouseEnter(pill.parentElement!);
		expect(screen.getByRole('dialog', { name: 'Scanner details' })).toBeInTheDocument();
		expect(screen.queryByRole('button', { name: 'Close scanner details' })).toBeNull();
		await fireEvent.mouseLeave(pill.parentElement!);
		expect(screen.queryByRole('dialog')).toBeNull();
	});

	it('stays away when there is nothing to pick', () => {
		stage({ Device: 'epson:1' }, []);
		expect(screen.queryByTestId('device-picker')).toBeNull();
	});

	it('names the scanner in the chips only when no picker is saying it already', () => {
		const { unmount } = stage({ Device: 'epson:1' });
		expect(screen.getByTestId('settings-chips')).not.toHaveTextContent('Epson DS-530');
		unmount();

		stage({ Device: 'epson:1' }, []);
		expect(screen.getByTestId('settings-chips')).toHaveTextContent('Epson DS-530');
	});

	it("offers a scanner its own presets and the shared ones, never another machine's", () => {
		presets.set([
			{ id: 'shared', name: 'Everywhere', icon: 'star', settings: { Dpi: 300 } },
			{ id: 'mine', name: 'Feeder run', icon: 'file', settings: { Dpi: 400 }, device: 'epson:1' },
			{
				id: 'theirs',
				name: 'Flatbed run',
				icon: 'image',
				settings: { Dpi: 600 },
				device: 'canon:2'
			}
		]);
		const { unmount } = stage({ Device: 'epson:1' });
		expect(screen.getByRole('button', { name: /Everywhere/ })).toBeInTheDocument();
		expect(screen.getByRole('button', { name: /Feeder run/ })).toBeInTheDocument();
		expect(screen.queryByRole('button', { name: /Flatbed run/ })).toBeNull();
		unmount();

		// Nothing chosen: only the presets that belong to no scanner in particular.
		stage({ Device: '' });
		expect(screen.getByRole('button', { name: /Everywhere/ })).toBeInTheDocument();
		expect(screen.queryByRole('button', { name: /Feeder run/ })).toBeNull();
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

	it('ticks the clean-ups the pages share, and toggles one at a time', async () => {
		const oncleanup = vi.fn();
		render(Toolbar, {
			selectedCount: 0,
			total: 2,
			anyDeleted: false,
			onrotate: vi.fn(),
			ondelete: vi.fn(),
			onrestore: vi.fn(),
			onfilter: vi.fn(),
			onclear: vi.fn(),
			cleanup: PageCleanup.AutoLevels,
			oncleanup
		});
		await fireEvent.click(screen.getByTestId('filter-button'));

		const despeckle = screen.getByRole('menuitemcheckbox', { name: /Despeckle/ });
		const levels = screen.getByRole('menuitemcheckbox', { name: /Auto-levels/ });
		expect(despeckle).toHaveAttribute('aria-checked', 'false');
		expect(levels).toHaveAttribute('aria-checked', 'true');

		await fireEvent.click(despeckle);
		expect(oncleanup).toHaveBeenLastCalledWith(PageCleanup.Despeckle, true);
		await fireEvent.click(levels);
		expect(oncleanup).toHaveBeenLastCalledWith(PageCleanup.AutoLevels, false);
		// Ticking stays in the menu: two clean-ups are often wanted together.
		expect(screen.getByTestId('filter-menu')).toBeInTheDocument();
	});

	it('keeps crop and straighten behind one Crop button', async () => {
		const oncrop = vi.fn();
		const onstraighten = vi.fn();
		render(Toolbar, {
			selectedCount: 0,
			total: 2,
			anyDeleted: false,
			onrotate: vi.fn(),
			ondelete: vi.fn(),
			onrestore: vi.fn(),
			onfilter: vi.fn(),
			onclear: vi.fn(),
			oncrop,
			onstraighten
		});

		await fireEvent.click(screen.getByTestId('crop-button'));
		await fireEvent.click(screen.getByTestId('straighten-open'));
		expect(onstraighten).toHaveBeenCalled();
		expect(screen.queryByTestId('crop-menu')).toBeNull();

		await fireEvent.click(screen.getByTestId('crop-button'));
		await fireEvent.click(screen.getByTestId('crop-open'));
		expect(oncrop).toHaveBeenCalled();
	});

	it('offers only auto-crop for several pages, and says when it is busy', async () => {
		const onautocrop = vi.fn();
		const props = {
			selectedCount: 3,
			total: 5,
			anyDeleted: false,
			onrotate: vi.fn(),
			ondelete: vi.fn(),
			onrestore: vi.fn(),
			onfilter: vi.fn(),
			onclear: vi.fn(),
			oncrop: vi.fn(),
			onstraighten: vi.fn(),
			onautocrop
		};
		const { rerender } = render(Toolbar, props);

		await fireEvent.click(screen.getByTestId('crop-button'));
		expect(screen.queryByTestId('crop-open')).toBeNull();
		expect(screen.queryByTestId('straighten-open')).toBeNull();
		expect(screen.getByTestId('autocrop')).toHaveTextContent('3 pages');
		await fireEvent.click(screen.getByTestId('autocrop'));
		expect(onautocrop).toHaveBeenCalledOnce();

		await rerender({ ...props, autocropping: true });
		await fireEvent.click(screen.getByTestId('crop-button'));
		expect(screen.getByTestId('autocrop')).toBeDisabled();
		expect(screen.getByTestId('autocrop')).toHaveTextContent('Finding borders');
	});

	it('offers expand only when asked to, and drops the whole bar while edits are off', async () => {
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

		await rerender({ ...props, onexpand });
		await fireEvent.click(screen.getByTestId('expand-button'));
		expect(onexpand).toHaveBeenCalled();

		// Disabled here means "not editable", e.g. mid-scan. Every edit goes, and a bar left
		// holding Expand alone is not worth showing, so there is no bar at all.
		await rerender({ ...props, onexpand, disabled: true });
		expect(screen.queryByTestId('toolbar')).toBeNull();
		expect(screen.queryByTestId('expand-button')).toBeNull();

		// The all-pages view keeps its bar for the selection, even with edits off.
		await rerender({ ...props, onexpand, disabled: true, scoped: true });
		expect(screen.getByTestId('toolbar-scope')).toBeVisible();

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
	it('swaps saving for a stop button while the scan runs, and holds it while stopping', async () => {
		const onstop = vi.fn();
		const { rerender } = render(ExportBar, {
			session: session(SessionState.Scanning),
			ondownload: vi.fn(),
			onstop
		});
		expect(screen.queryByTestId('download-button')).toBeNull();
		await fireEvent.click(screen.getByTestId('stop-scan-button'));
		expect(onstop).toHaveBeenCalledOnce();

		await rerender({
			session: session(SessionState.Scanning),
			ondownload: vi.fn(),
			onstop,
			stopping: true
		});
		expect(screen.getByTestId('stop-scan-button')).toBeDisabled();
		expect(screen.getByTestId('stop-scan-button')).toHaveTextContent('Stopping…');

		await rerender({ session: session(SessionState.Review, 3), ondownload: vi.fn(), onstop });
		expect(screen.queryByTestId('stop-scan-button')).toBeNull();
		expect(screen.getByTestId('download-button')).toBeInTheDocument();
	});

	it('says what the one download button will do, and asks for a place when it can', async () => {
		const ondownload = vi.fn();
		const { rerender } = render(ExportBar, { session: session(), ondownload });
		expect(screen.getByTestId('download-button')).toHaveTextContent('Download');

		await rerender({ session: session(), picker: true, ondownload });
		expect(screen.getByTestId('download-button')).toHaveTextContent('Save as…');
		await fireEvent.click(screen.getByTestId('download-button'));
		expect(ondownload).toHaveBeenCalled();
	});

	it('offers Save to server beside the download only when given a folder', async () => {
		const onserver = vi.fn();
		const { rerender } = render(ExportBar, { session: session(), ondownload: vi.fn(), onserver });
		expect(screen.queryByTestId('server-save-button')).toBeNull();

		await rerender({
			session: session(),
			ondownload: vi.fn(),
			onserver,
			serverFolder: '/data/consume'
		});
		expect(screen.getByTestId('download-button')).toBeInTheDocument();
		await fireEvent.click(screen.getByTestId('server-save-button'));
		expect(onserver).toHaveBeenCalled();
		expect(screen.getByTestId('server-save-button')).toHaveAttribute(
			'title',
			expect.stringContaining('/data/consume')
		);
	});

	it('disables the download while scanning or with everything deleted', () => {
		const { rerender } = render(ExportBar, {
			session: session(SessionState.Scanning),
			ondownload: vi.fn()
		});
		expect(screen.getByTestId('download-button')).toBeDisabled();
		rerender({ session: session(SessionState.Review, 3), ondownload: vi.fn() });
		expect(screen.getByTestId('download-button')).toBeDisabled();
	});

	it('shows progress while the document is being prepared', () => {
		render(ExportBar, {
			session: session(SessionState.Exporting),
			progress: 40,
			ondownload: vi.fn()
		});
		expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '40');
		expect(screen.queryByTestId('download-button')).toBeNull();
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

	it('leads with the source and ends with the format', () => {
		render(SettingsChips, { options, format: 'PDF', onclick: vi.fn() });
		const chips = [...screen.getByTestId('settings-chips').children].map((c) =>
			c.textContent?.trim()
		);
		expect(chips[0]).toBe('Feeder');
		expect(chips.at(-1)).toBe('PDF');
		expect(chips).toContain('300 dpi');
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

describe('TemplateEditor', () => {
	const editor = (tokens: readonly string[]) => ({
		value: '{YEAR}-{MONTH}-{DAY}',
		tokens,
		preview: '2026-09-24',
		unknown: [],
		onchange: vi.fn()
	});

	it('offers a chip per token it is handed, and none it is not', async () => {
		const { rerender } = render(TemplateEditor, editor(['YEAR', 'MONTH', 'DAY']));
		expect(screen.getByRole('button', { name: '{DAY}' })).toBeInTheDocument();

		await rerender(editor(['YEAR', 'MONTH']));
		expect(screen.queryByRole('button', { name: '{DAY}' })).toBeNull();
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
});

describe('AdjustModal', () => {
	const base = { src: '/x.webp', alt: 'Page 1', adjust: null, onapply: vi.fn(), oncancel: vi.fn() };

	it('previews the change with CSS and applies the draft', async () => {
		const onapply = vi.fn();
		render(AdjustModal, { ...base, onapply });
		const apply = screen.getByTestId('adjust-apply');
		expect(apply).toBeDisabled();

		await fireEvent.input(screen.getByLabelText('Brightness'), { target: { value: '50' } });

		expect(screen.getByTestId('adjust-preview').style.filter).toContain('brightness(1.250)');
		await fireEvent.click(apply);
		expect(onapply).toHaveBeenCalledWith({ Brightness: 50, Contrast: 0, Sharpen: 0 });
	});

	it('previews only the difference from what the page already has', async () => {
		render(AdjustModal, { ...base, adjust: { Brightness: 50, Contrast: 0, Sharpen: 0 } });

		await fireEvent.input(screen.getByLabelText('Brightness'), { target: { value: '0' } });

		// The preview image is already 1.25 bright; back to 0 means scaling it by 1/1.25.
		expect(screen.getByTestId('adjust-preview').style.filter).toContain('brightness(0.800)');
	});

	it('sends a reset to neutral as no adjust at all, and cancels on Escape', async () => {
		const onapply = vi.fn();
		const oncancel = vi.fn();
		render(AdjustModal, {
			...base,
			adjust: { Brightness: 10, Contrast: 20, Sharpen: 30 },
			onapply,
			oncancel
		});

		await fireEvent.click(screen.getByTestId('adjust-reset'));
		await fireEvent.click(screen.getByTestId('adjust-apply'));
		expect(onapply).toHaveBeenCalledWith(undefined);

		await fireEvent.keyDown(screen.getByTestId('adjust-modal'), { key: 'Escape' });
		expect(oncancel).toHaveBeenCalled();
	});

	it('says how many pages it sets, and lets a selection be set to the same values', () => {
		render(AdjustModal, { ...base, count: 3 });

		expect(screen.getByRole('dialog', { name: 'Adjust 3 pages' })).toBeInTheDocument();
		expect(screen.getByTestId('adjust-apply')).toBeEnabled();
	});
});

describe('StraightenModal', () => {
	const base = { src: '/x.webp', alt: 'Page 1', skew: 0, onapply: vi.fn(), oncancel: vi.fn() };

	it('turns the preview by the change, nudges in tenths and applies the angle', async () => {
		const onapply = vi.fn();
		render(StraightenModal, { ...base, skew: 1.5, onapply });
		expect(screen.getByTestId('straighten-apply')).toBeDisabled();

		await fireEvent.click(screen.getByTestId('straighten-right'));
		await fireEvent.click(screen.getByTestId('straighten-right'));

		expect(screen.getByTestId('straighten-preview').style.transform).toMatch(
			/^rotate\(0\.2\d*deg\)/
		);
		await fireEvent.click(screen.getByTestId('straighten-apply'));
		expect(onapply).toHaveBeenCalledWith(1.7);
	});

	it('stops at the limit and cancels on Escape', async () => {
		const oncancel = vi.fn();
		render(StraightenModal, { ...base, skew: 15, oncancel });

		expect(screen.getByTestId('straighten-right')).toBeDisabled();
		await fireEvent.keyDown(screen.getByTestId('straighten-modal'), { key: 'Escape' });
		expect(oncancel).toHaveBeenCalled();
	});
});

describe('Preview arrows', () => {
	const pages = [page(0), page(1), page(2)];
	const base = { sessionId: 's', pages, onnavigate: vi.fn(), onclose: vi.fn(), onrotate: vi.fn() };

	it('shows only the arrows that have a page to go to', () => {
		const { rerender } = render(Preview, { ...base, index: 0 });
		expect(screen.queryByTestId('preview-prev')).toBeNull();
		expect(screen.getByTestId('preview-next')).toBeInTheDocument();

		rerender({ ...base, index: 1 });
		expect(screen.getByTestId('preview-prev')).toBeInTheDocument();
		expect(screen.getByTestId('preview-next')).toBeInTheDocument();

		rerender({ ...base, index: 2 });
		expect(screen.getByTestId('preview-prev')).toBeInTheDocument();
		expect(screen.queryByTestId('preview-next')).toBeNull();
	});
});

describe('Preview edit bar', () => {
	const pages = [page(0), page(1, { Cleanup: PageCleanup.Despeckle })];
	const base = {
		sessionId: 's',
		pages,
		index: 1,
		onnavigate: vi.fn(),
		onclose: vi.fn(),
		onrotate: vi.fn()
	};

	it('edits the page on screen', async () => {
		const onadjust = vi.fn();
		const onstraighten = vi.fn();
		const oncleanup = vi.fn();
		render(Preview, {
			...base,
			editable: true,
			onadjust,
			oncrop: vi.fn(),
			onstraighten,
			onfilter: vi.fn(),
			oncleanup
		});

		await fireEvent.click(screen.getByTestId('preview-adjust'));
		expect(onadjust).toHaveBeenCalledWith(1);
		await fireEvent.click(screen.getByTestId('preview-straighten'));
		expect(onstraighten).toHaveBeenCalledWith(1);

		await fireEvent.click(screen.getByTestId('preview-filter'));
		const despeckle = screen.getByRole('menuitemcheckbox', { name: /Despeckle/ });
		expect(despeckle).toHaveAttribute('aria-checked', 'true');
		await fireEvent.click(despeckle);
		expect(oncleanup).toHaveBeenCalledWith(1, PageCleanup.Despeckle, false);
	});

	it('has no edit bar while the page cannot be edited', () => {
		render(Preview, { ...base, onadjust: vi.fn() });

		expect(screen.queryByTestId('preview-edit-bar')).toBeNull();
	});

	it('leaves the keys to a dialog open over it', async () => {
		const onclose = vi.fn();
		const onnavigate = vi.fn();
		render(Preview, { ...base, onclose, onnavigate });

		// Something opens over the preview, as a dialog does: it is the newest layer.
		const closeDialog = vi.fn();
		const dialog = layer(closeDialog);
		await fireEvent.keyDown(window, { key: 'ArrowLeft' });
		await fireEvent.keyDown(window, { key: 'Escape' });
		expect(closeDialog).toHaveBeenCalledOnce();
		expect(onclose).not.toHaveBeenCalled();
		expect(onnavigate).not.toHaveBeenCalled();

		// With it gone, the keys are the preview's again.
		dialog.pop();
		await fireEvent.keyDown(window, { key: 'ArrowLeft' });
		expect(onnavigate).toHaveBeenCalled();
		await fireEvent.keyDown(window, { key: 'Escape' });
		expect(onclose).toHaveBeenCalled();
	});
});

describe('CropModal', () => {
	it('draws a turned page upright and hands the crop back in scan coordinates', async () => {
		const onapply = vi.fn();
		render(CropModal, {
			src: '/frame.webp',
			alt: 'Page 1',
			crop: { Top: 0, Right: 0, Bottom: 0, Left: 10 },
			rotation: Rotation.R90,
			onapply,
			oncancel: vi.fn()
		});

		// Turned right, the scan's left edge is the top of the page on screen.
		expect(screen.getByTestId('crop-top')).toHaveValue('10');
		expect(screen.getByTestId('crop-left')).toHaveValue('0');

		await fireEvent.input(screen.getByTestId('crop-right'), { target: { value: '5' } });
		await fireEvent.click(screen.getByTestId('crop-apply'));

		expect(onapply).toHaveBeenCalledWith({ Top: 5, Right: 0, Bottom: 0, Left: 10 });
	});
});

describe('PresetPills', () => {
	it('offers to save settings that match no preset, as one plain button', async () => {
		const onsave = vi.fn();
		const props = { presets: [], active: null, onpick: vi.fn(), onsave };
		const { rerender } = render(PresetPills, { ...props, modified: false });
		expect(screen.queryByTestId('preset-save-current')).toBeNull();

		await rerender({ ...props, modified: true });
		const save = screen.getByRole('button', { name: 'Save as preset' });
		await fireEvent.click(save);
		expect(onsave).toHaveBeenCalled();
	});
});
