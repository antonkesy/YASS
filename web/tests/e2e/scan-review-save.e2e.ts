import { expect, test, type Page } from '@playwright/test';

// The server runs `--fake pages=3,blank=2,delay=150ms` (see playwright.config.ts).

async function scanToReview(page: Page) {
	await page.goto('/');
	await expect(page.getByTestId('status-pill')).toHaveText(/Ready/);
	await page.getByTestId('scan-button').click();
	await expect(page).toHaveURL(/\/session\/[A-Za-z0-9-]+$/);
	await expect(page.getByTestId('session-state')).toHaveText('Ready to review', {
		timeout: 20_000
	});
	await expect(page.getByTestId('page-thumb')).toHaveCount(3);
}

/**
 * A browser with no save dialog to offer: what Firefox and Safari are, and what any
 * box served over plain http on a LAN is, secure contexts being the API's price.
 */
async function withoutSavePicker(page: Page) {
	await page.addInitScript(() => {
		Reflect.deleteProperty(window, 'showSaveFilePicker');
		Reflect.deleteProperty(window, 'showDirectoryPicker');
	});
}

test('scan, review, edit, download and save', async ({ page }) => {
	await withoutSavePicker(page);
	await scanToReview(page);

	// Blank page is pre-marked deleted with a badge; bottom bar counts active pages.
	const thumbs = page.getByTestId('page-thumb');
	await expect(thumbs.nth(1)).toHaveAttribute('data-deleted', 'true');
	await expect(thumbs.nth(1).getByTestId('blank-badge')).toBeVisible();
	await expect(page.getByTestId('page-count')).toHaveText('2 of 3 pages');

	// The strip only shows pages; ticking them is the all-pages overview's job.
	await expect(thumbs.nth(0).getByTestId('page-select')).toHaveCount(0);
	const firstImg = thumbs.nth(0).locator('img');
	await expect(firstImg).toBeVisible();
	const before = await firstImg.getAttribute('src');

	// Tick page 1 over there and rotate from the toolbar that comes with it: the edit
	// acts on the selection only, and the hash changes after the round trip.
	await page.getByTestId('overview-open').click();
	const overview = page.getByTestId('page-overview');
	const all = overview.getByTestId('page-thumb');
	await all.nth(0).getByTestId('page-select').click();
	await expect(overview.getByTestId('toolbar-scope')).toHaveText(/1 selected/);
	await overview.getByRole('button', { name: 'Rotate right' }).click();
	await expect(firstImg).not.toHaveAttribute('src', before!);
	await expect(all.nth(2).locator('img')).toHaveAttribute('src', /h=[0-9a-f]{16}/);

	// Restore the blank page, then delete it again, without leaving the overview.
	await overview.getByRole('button', { name: 'Clear' }).click();
	await all.nth(1).getByTestId('page-select').click();
	await overview.getByTestId('restore-button').click();
	await expect(all.nth(1)).toHaveAttribute('data-deleted', 'false');
	await expect(page.getByTestId('page-count')).toHaveText('3 of 3 pages');
	await overview.getByTestId('delete-button').click();
	await expect(all.nth(1)).toHaveAttribute('data-deleted', 'true');

	// Select all takes the lot, and the toolbar's Clear is the only way back.
	await overview.getByRole('button', { name: 'Clear' }).click();
	await overview.getByTestId('overview-select-all').click();
	await expect(overview.getByTestId('toolbar-scope')).toHaveText(/3 selected/);
	await expect(overview.getByTestId('overview-select-all')).toBeDisabled();
	await overview.getByRole('button', { name: 'Clear' }).click();
	await page.getByTestId('overview-close').click();
	await expect(overview).toHaveCount(0);

	// Tapping the page itself only moves the stage; nothing joins the selection.
	await thumbs.nth(0).getByRole('button', { name: 'Show page 1' }).click();
	await expect(thumbs.nth(0)).toHaveAttribute('data-current', 'true');
	await expect(page.getByTestId('toolbar-scope')).toHaveText(/all 3/);
	// The strip shows pages; opening one full screen is the toolbar's Expand.
	await expect(thumbs.nth(0).getByRole('button', { name: 'Open page 1' })).toHaveCount(0);

	// Full-page preview of whatever the stage is showing, with next/prev and keyboard.
	await page.getByTestId('expand-button').click();
	const preview = page.getByTestId('preview');
	await expect(preview).toBeVisible();
	await expect(page.getByTestId('preview-counter')).toHaveText('1 / 3');

	// Zoom fills the viewport width instead of fitting the page; moving on resets it.
	const zoom = page.getByTestId('preview-zoom');
	const previewImg = preview.locator('img');
	const fitted = await previewImg.boundingBox();
	await zoom.click();
	await expect(zoom).toHaveAttribute('aria-pressed', 'true');
	const enlarged = await previewImg.boundingBox();
	expect(enlarged!.width).toBeGreaterThan(fitted!.width);

	await page.getByTestId('preview-next').click();
	await expect(zoom).toHaveAttribute('aria-pressed', 'false');
	await expect(page.getByTestId('preview-counter')).toHaveText(/2 \/ 3/);
	await page.keyboard.press('ArrowRight');
	await expect(page.getByTestId('preview-counter')).toHaveText('3 / 3');
	await page.keyboard.press('Escape');
	await expect(preview).toBeHidden();

	// Naming the scan is what the past scans list shows instead of the timestamp.
	await page.getByTestId('name-input').fill('Tax return 2025');
	await page.getByTestId('name-input').press('Enter');

	// With no save dialog to offer, the one button is a plain download of a real one-shot PDF.
	await expect(page.getByTestId('download-button')).toHaveText(/Download/);
	const download = page.waitForEvent('download');
	await page.getByTestId('download-button').click();
	const file = await download;
	expect(file.suggestedFilename()).toMatch(/\.pdf$/);

	// Save finishes the session.
	await page.getByTestId('save-button').click();
	await expect(page.getByTestId('saved-message')).toBeVisible({ timeout: 20_000 });
	await expect(page.getByTestId('session-state')).toHaveText('Saved');

	// Home lists it as exported with the active page count. Save writes to the configured
	// output directory, which is what "Exported" means; "Saved" is a chosen folder.
	await page.getByRole('link', { name: 'Back to home' }).click();
	const list = page.getByTestId('session-list');
	await expect(list.getByTestId('session-status').first()).toHaveText('Exported');
	await expect(list.getByTestId('session-tile').first()).toContainText('2 p');
	await expect(list.getByTestId('session-tile').first()).toContainText('Tax return 2025');
});

test('the all-pages overview zooms, reorders by drag and hands the order back', async ({
	page
}) => {
	await scanToReview(page);

	await page.getByTestId('overview-open').click();
	const overview = page.getByTestId('page-overview');
	await expect(overview).toBeVisible();
	const thumbs = overview.getByTestId('page-thumb');
	await expect(thumbs).toHaveCount(3);

	// Zoom is a step at a time, and the tiles actually grow with it.
	const small = (await thumbs.nth(0).boundingBox())!;
	await page.getByTestId('overview-zoom-in').click();
	await expect
		.poll(async () => (await thumbs.nth(0).boundingBox())!.width)
		.toBeGreaterThan(small.width);

	// Back down to the smallest step, where the whole scan fits on a phone screen too.
	const zoomOut = page.getByTestId('overview-zoom-out');
	while (await zoomOut.isEnabled()) await zoomOut.click();

	// Drag the blank page, which starts deleted, to the front of the scan.
	await expect(thumbs.nth(0)).toHaveAttribute('data-deleted', 'false');
	await expect(thumbs.nth(1)).toHaveAttribute('data-deleted', 'true');
	const blank = (await thumbs.nth(1).boundingBox())!;
	const first = (await thumbs.nth(0).boundingBox())!;
	await page.mouse.move(blank.x + blank.width / 2, blank.y + blank.height / 2);
	await page.mouse.down();
	// A finger has to hold before it drags, so that a swipe still scrolls; a mouse does
	// not wait, and the pause costs it nothing.
	await page.waitForTimeout(350);
	await page.mouse.move(first.x + first.width / 2, first.y + first.height / 2, { steps: 12 });
	await page.mouse.up();

	await expect(thumbs.nth(0)).toHaveAttribute('data-deleted', 'true');
	await expect(thumbs.nth(1)).toHaveAttribute('data-deleted', 'false');

	// Closing leaves the filmstrip showing the order the overview settled on.
	await page.getByTestId('overview-close').click();
	await expect(overview).toHaveCount(0);
	await expect(page.getByTestId('page-thumb').nth(0)).toHaveAttribute('data-deleted', 'true');
});

test('settings only expose ui_editable keys and are applied to the next scan', async ({ page }) => {
	await page.goto('/settings');
	const form = page.getByTestId('settings-form');
	const dpi = form.getByTestId('seg-dpi');
	await expect(dpi).toBeVisible();
	await expect(form.getByTestId('seg-brightness')).toHaveCount(0);
	await dpi.getByRole('radio', { name: '150', exact: true }).check();
	await expect(page.getByTestId('overrides-active')).toBeVisible();

	await page.goto('/settings');
	await expect(dpi.getByRole('radio', { name: '150', exact: true })).toBeChecked();
	await page.getByTestId('reset-settings').click();
	await expect(page.getByTestId('overrides-active')).toBeHidden();
	await expect(dpi.getByRole('radio', { name: 'Default', exact: true })).toBeChecked();
});

test('discard keeps the scan listed as discarded', async ({ page }) => {
	await scanToReview(page);
	await page.getByTestId('discard-button').click();
	await expect(page.getByTestId('discard-button')).toHaveText('Really discard?');
	await page.getByTestId('discard-button').click();
	await expect(page).toHaveURL(/\/$/);

	// Discarding throws the pages away but leaves the row, unlike deleting.
	const list = page.getByTestId('session-list');
	await expect(list.getByTestId('session-status').first()).toHaveText('Discarded');
});

test('stop ends a scan early', async ({ page }) => {
	await page.goto('/');
	await page.getByTestId('scan-button').click();
	// Forced: the review screen is still sliding in while the fake feeder is already
	// half done, and waiting for the button to stop moving can outlast the scan itself.
	await page.getByTestId('stop-button').click({ timeout: 5_000, force: true });
	await expect(page.getByTestId('session-state')).toHaveText('Ready to review', {
		timeout: 20_000
	});
	await expect(page.getByTestId('stop-button')).toBeHidden();
});

test('save as… writes the document where the save dialog points', async ({ page }) => {
	type Saved = { name: string | null; bytes: number };
	await page.addInitScript(() => {
		const saved: Saved = { name: null, bytes: 0 };
		Object.assign(window, {
			__saved: saved,
			// Stands in for the dialog: Playwright cannot drive a native one, but what the
			// page does with the handle it gets back is the part worth testing.
			showSaveFilePicker: (options: { suggestedName?: string }) => {
				saved.name = options.suggestedName ?? null;
				return Promise.resolve({
					createWritable: () =>
						Promise.resolve({
							write: (data: Blob) => {
								saved.bytes += data.size;
								return Promise.resolve();
							},
							close: () => Promise.resolve()
						})
				});
			},
			showDirectoryPicker: () => Promise.reject(new Error('one document needs no folder'))
		});
	});
	await scanToReview(page);
	await page.getByTestId('name-input').fill('Tax return 2025');
	await page.getByTestId('name-input').press('Enter');

	await expect(page.getByTestId('download-button')).toHaveText(/Save as…/);
	await page.getByTestId('download-button').click();

	const saved = () => page.evaluate(() => (window as unknown as { __saved: Saved }).__saved);
	// A two-page PDF of blank fake pages is small, but it is the whole document.
	await expect.poll(async () => (await saved()).bytes, { timeout: 20_000 }).toBeGreaterThan(500);
	expect((await saved()).name).toBe('Tax return 2025.pdf');
	// Handing the document over finishes the session, the same as Export does.
	await expect(page.getByTestId('session-state')).toHaveText('Saved');
});

test('scan more appends pages and export writes to the configured folder', async ({ page }) => {
	await scanToReview(page);

	// The dashed tile at the end of the filmstrip runs the feeder again.
	await page.getByTestId('scan-more-tile').click();
	await expect(page.getByTestId('page-thumb')).toHaveCount(6, { timeout: 30_000 });
	await expect(page.getByTestId('session-state')).toHaveText('Ready to review', {
		timeout: 30_000
	});
	// 4 of 6: page 2 of each run comes out blank and starts deleted.
	await expect(page.getByTestId('page-count')).toHaveText('4 of 6 pages');

	// Export writes into output.directory, which is where the path under the button points.
	await expect(page.getByTestId('export-path')).toBeVisible();
	await page.getByTestId('save-button').click();
	await expect(page.getByTestId('saved-message')).toBeVisible({ timeout: 20_000 });
	await expect(page.getByTestId('session-state')).toHaveText('Saved');
});

test('turning the export button off promotes the download button in its place', async ({
	page
}) => {
	await page.goto('/settings');
	const toggle = page.getByTestId('toggle-export-button');
	await expect(toggle).toBeVisible();
	await toggle.click();

	await scanToReview(page);
	await expect(page.getByTestId('save-button')).toHaveCount(0);
	await expect(page.getByTestId('export-path')).toHaveCount(0);
	await expect(page.getByTestId('download-button')).toBeVisible();

	// Put it back so the following tests see the default sidebar.
	await page.goto('/settings');
	await page.getByTestId('toggle-export-button').click();
});
