import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import StatusPill from '$lib/components/StatusPill.svelte';
import Toolbar from '$lib/components/Toolbar.svelte';
import ExportBar from '$lib/components/ExportBar.svelte';
import PageThumb from '$lib/components/PageThumb.svelte';
import SettingsChips from '$lib/components/SettingsChips.svelte';
import SortMenu from '$lib/components/SortMenu.svelte';
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
		expect(screen.getByRole('status')).toHaveTextContent('Ready');
		rerender({ status: null, connection: 'reconnecting' });
		expect(screen.getByRole('status')).toHaveTextContent('Offline');
	});
});

describe('Toolbar', () => {
	it('acts on all pages when nothing is selected and emits edits', async () => {
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
		expect(screen.getByTestId('toolbar-scope')).toHaveTextContent('all 4');
		await fireEvent.click(screen.getByRole('button', { name: 'Rotate right' }));
		expect(onrotate).toHaveBeenCalledWith(90);
		expect(screen.getByTestId('restore-button')).toBeDisabled();
		await fireEvent.click(screen.getByTestId('filter-button'));
		await fireEvent.click(screen.getByRole('menuitem', { name: 'Black & white' }));
		expect(onfilter).toHaveBeenCalledWith(PageFilter.Bw);
	});

	it('shows the selection count and a clear button', async () => {
		const onclear = vi.fn();
		render(Toolbar, {
			selectedCount: 2,
			total: 4,
			anyDeleted: true,
			onrotate: vi.fn(),
			ondelete: vi.fn(),
			onrestore: vi.fn(),
			onfilter: vi.fn(),
			onclear
		});
		expect(screen.getByTestId('toolbar-scope')).toHaveTextContent('2 selected');
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
		expect(screen.getByTestId('page-count')).toHaveTextContent('3 of 3 pages');
		expect(screen.getByTestId('export-path')).toHaveTextContent('/data/consume');
		expect(screen.getByTestId('autosave-strip')).toHaveTextContent('Auto-saved to past scans');
		// null, not the path on screen: Export means "wherever the preference points".
		await fireEvent.click(screen.getByTestId('save-button'));
		expect(onsave).toHaveBeenCalledWith(null);
	});

	it('picks a folder from the save-to menu and passes a typed one through', async () => {
		const onsave = vi.fn();
		render(ExportBar, {
			session: session(),
			roots: ['/data/consume'],
			recent: ['/mnt/nas/scans'],
			onsave,
			ondownload: vi.fn()
		});
		expect(screen.queryByTestId('save-to-menu')).toBeNull();
		await fireEvent.click(screen.getByTestId('save-to-button'));
		await fireEvent.click(screen.getByRole('button', { name: /mnt\/nas\/scans/ }));
		expect(onsave).toHaveBeenCalledWith('/mnt/nas/scans');
		expect(screen.queryByTestId('save-to-menu')).toBeNull();

		await fireEvent.click(screen.getByTestId('save-to-button'));
		const custom = screen.getByTestId('save-to-custom');
		await fireEvent.input(custom, { target: { value: ' /tmp/out/ ' } });
		await fireEvent.click(screen.getByRole('button', { name: 'Save here' }));
		expect(onsave).toHaveBeenLastCalledWith('/tmp/out/');
	});

	it('promotes save to… to the primary action when the export button is off', () => {
		render(ExportBar, {
			session: session(),
			exportEnabled: false,
			exportDirectory: '/data/consume',
			onsave: vi.fn(),
			ondownload: vi.fn()
		});
		expect(screen.queryByTestId('save-button')).toBeNull();
		expect(screen.queryByTestId('export-path')).toBeNull();
		expect(screen.getByTestId('save-to-button')).toBeInTheDocument();
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
	it('toggles selection on tap, opens on the expand button, and badges blank/deleted pages', async () => {
		const ontoggle = vi.fn();
		const onopen = vi.fn();
		const { container } = render(PageThumb, {
			sessionId: 's1',
			page: page(2, { Deleted: true, IsBlank: true, Hash: 'abc' }),
			ontoggle,
			onopen
		});
		expect(screen.getByTestId('blank-badge')).toBeInTheDocument();
		expect(screen.getByTestId('page-thumb')).toHaveAttribute('data-deleted', 'true');
		expect(container.querySelector('img')).toHaveAttribute(
			'src',
			expect.stringContaining('/img/s1/2/thumb?h=abc')
		);
		await fireEvent.click(screen.getByRole('button', { name: /^Page 3/ }));
		expect(ontoggle).toHaveBeenCalledWith(2);
		await fireEvent.click(screen.getByRole('button', { name: 'Open page 3' }));
		expect(onopen).toHaveBeenCalledWith(2);
	});

	it('shows a spinner until the thumbnail exists', () => {
		const { container } = render(PageThumb, {
			sessionId: 's1',
			page: page(0, { Hash: 'h', ThumbnailReady: false }),
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
		expect(screen.getByTestId('sort-button')).toHaveTextContent('Newest');
		await rerender({ order: 'name', onchange });
		expect(screen.getByTestId('sort-button')).toHaveTextContent('Name');
	});
});
