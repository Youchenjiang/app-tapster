#!/usr/bin/env python3
"""
Microsoft Store Publishing Script for Tapster

Automates package upload, metadata synchronization from docs/StoreListing_*.md,
and submission management via Microsoft Partner Center Submission API.
"""

import copy
import io
import json
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import zipfile

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')
if hasattr(sys.stderr, 'reconfigure'):
    sys.stderr.reconfigure(encoding='utf-8')

LOCALE_MAP = {
    'ZH': 'zh-tw',
    'ZH-CN': 'zh-cn',
    'EN': 'en-us',
    'JA': 'ja-jp',
    'KO': 'ko-kr'
}

IMAGE_DIR_NAME = os.path.join('packaging', 'store_assets', 'screenshots')
APP_PACKAGES_KEY = 'applicationPackages'
MAX_SCREENSHOTS_PER_LISTING = 10

IMAGE_FILE_MAP = {
    'tw1.png': ('zh-tw', 'Screenshot'),
    'tw2.png': ('zh-tw', 'Screenshot'),
    'cn1.png': ('zh-cn', 'Screenshot'),
    'cn2.png': ('zh-cn', 'Screenshot'),
    'en1.png': ('en-us', 'Screenshot'),
    'en2.png': ('en-us', 'Screenshot'),
    'ja1.png': ('ja-jp', 'Screenshot'),
    'ja2.png': ('ja-jp', 'Screenshot'),
    'ko1.png': ('ko-kr', 'Screenshot'),
    'ko2.png': ('ko-kr', 'Screenshot'),
}

ACCEPTED_STATUSES = ('PreProcessing', 'Certification', 'Published')
IN_FLIGHT_STATUSES = ('CommitStarted',)


def is_dry_run():
    return '--dry-run' in sys.argv


def open_https(request, timeout=60):
    target = request.full_url if isinstance(request, urllib.request.Request) else request
    if not target.lower().startswith('https://'):
        raise ValueError(f'Only HTTPS URLs are allowed: {target}')
    return urllib.request.urlopen(request, timeout=timeout)


def _find_latest_msix(repo_root):
    publish_dir = os.path.join(repo_root, "publish")
    if not os.path.exists(publish_dir):
        return None
    candidates = [
        os.path.join(publish_dir, f)
        for f in os.listdir(publish_dir)
        if f.startswith("Tapster") and f.endswith(".msix")
    ]
    if candidates:
        candidates.sort(key=os.path.getmtime, reverse=True)
        return candidates[0]
    return None


def _read_config_file(config_path):
    if not os.path.exists(config_path):
        return {}
    with open(config_path, 'r', encoding='utf-8-sig') as f:
        return json.load(f)


def load_store_config(config_path):
    file_cfg = _read_config_file(config_path)

    t_id = os.environ.get("STORE_TENANT_ID") or file_cfg.get("TenantId") or file_cfg.get("tenantId")
    c_id = os.environ.get("STORE_CLIENT_ID") or file_cfg.get("ClientId") or file_cfg.get("clientId")
    c_sec = os.environ.get("STORE_CLIENT_SECRET") or file_cfg.get("ClientSecret") or file_cfg.get("clientSecret")
    p_id = os.environ.get("STORE_PRODUCT_ID") or file_cfg.get("ProductId") or file_cfg.get("productId")
    m_path = os.environ.get("STORE_MSIX_PATH") or file_cfg.get("MsixPath") or file_cfg.get("msixPath")

    if not m_path:
        repo_root = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
        m_path = _find_latest_msix(repo_root)

    if is_dry_run():
        return (
            t_id or "MOCK_TENANT",
            c_id or "MOCK_CLIENT",
            c_sec or "MOCK_SECRET",
            p_id or "MOCK_PRODUCT",
            m_path or "publish/Tapster.msix"
        )

    if not all([t_id, c_id, c_sec, p_id, m_path]):
        print("Error: Missing credentials or paths. Please provide scripts/local_store_config.json or STORE_* environment variables.")
        sys.exit(1)

    return t_id, c_id, c_sec, p_id, m_path


def get_token(t_id, c_id, c_sec):
    print("Acquiring Microsoft Store Access Token...")
    if is_dry_run():
        return "MOCK_TOKEN"
    token_url = f'https://login.microsoftonline.com/{t_id}/oauth2/token'
    token_data = urllib.parse.urlencode({
        'grant_type': 'client_credentials',
        'client_id': c_id,
        'client_secret': c_sec,
        'resource': 'https://manage.devcenter.microsoft.com'
    }).encode()

    for attempt in range(1, 4):
        req = urllib.request.Request(token_url, data=token_data)
        try:
            with open_https(req, timeout=30) as resp:
                data = json.loads(resp.read().decode())
                token = data.get('access_token')
                if token:
                    return token
        except Exception as e:
            print(f"  Token attempt {attempt} failed: {e}")
            if attempt < 3:
                time.sleep(10)
    print("ERROR: Failed to acquire Access Token after 3 attempts.")
    sys.exit(1)


def build_api_request(url, token, method, body_dict, headers):
    req_headers = {'Authorization': f'Bearer {token}'}
    if headers:
        req_headers.update(headers)
    data = None
    if body_dict is not None:
        data = json.dumps(body_dict, ensure_ascii=False).encode('utf-8')
        req_headers.setdefault('Content-Type', 'application/json')
    return urllib.request.Request(url, data=data, headers=req_headers, method=method)


def api_request(url, token, method='GET', body_dict=None, headers=None, retries=5, delay=15):
    if is_dry_run():
        return {"status": "DryRun", "id": "MOCK_SUBMISSION", "fileUploadUrl": "https://example.com/mock_sas"}

    current_delay = delay
    for attempt in range(1, retries + 1):
        req = build_api_request(url, token, method, body_dict, headers)
        try:
            with open_https(req, timeout=180) as resp:
                resp_bytes = resp.read()
                return json.loads(resp_bytes.decode()) if resp_bytes else {}
        except urllib.error.HTTPError as error:
            body = error.read().decode('utf-8', errors='replace')
            print(f"  API {method} {url} attempt {attempt} failed with HTTP {error.code}: {body[:250]}")
            if error.code not in (429, 500, 502, 503, 504):
                return None
        except Exception as error:
            print(f"  API {method} {url} attempt {attempt} failed: {error}")
        if attempt < retries:
            time.sleep(current_delay)
            current_delay *= 2
    return None


def match_section_name(header):
    h = header.lower()
    if any(k in h for k in ("short", "簡短", "简短", "簡単", "간단")):
        return 'shortDescription'
    if any(k in h for k in ("description", "產品描述", "詳細描述", "詳細說明", "完整描述", "説明", "설명")):
        return 'description'
    if any(k in h for k in ("features", "主要功能", "功能亮點", "產品亮點", "产品亮点", "特性", "特徴", "主な機能", "제품 기능")):
        return 'features'
    if any(k in h for k in ("what's new", "更新日誌", "新功能說明", "新機能", "새로운 기능")):
        return 'releaseNotes'
    if any(k in h for k in ("keywords", "terms", "關鍵字", "关键字", "キーワード", "키워드")):
        return 'keywords'
    return None


def parse_markdown_listing(file_path):
    if not os.path.exists(file_path):
        return None
    with open(file_path, 'r', encoding='utf-8') as f:
        lines = f.readlines()

    sections = {}
    current_section = None
    for line in lines:
        stripped = line.strip()
        if stripped.startswith("## "):
            current_section = match_section_name(stripped[3:].strip())
            if current_section:
                sections[current_section] = []
        elif current_section:
            sections[current_section].append(line)

    listing = {
        'description': "".join(sections.get('description', [])).strip(),
        'shortDescription': "".join(sections.get('shortDescription', [])).strip(),
        'releaseNotes': "".join(sections.get('releaseNotes', [])).strip(),
        'features': [],
        'keywords': []
    }

    if 'features' in sections:
        items = [re.sub(r'^[-\*\d\.]+\s*', '', l).strip() for l in sections['features'] if l.strip()]
        listing['features'] = items[:20]

    if 'keywords' in sections:
        raw_kw = "\n".join(sections['keywords'])
        parts = re.split(r'[,，\n]+', raw_kw)
        kw_items = [re.sub(r'^[-\*\d\.]+\s*', '', p).strip() for p in parts if p.strip()]
        listing['keywords'] = kw_items[:7]

    return listing


def parse_all_listings(repo_root):
    docs_dir = os.path.join(repo_root, "docs")
    parsed_listings = {}
    for prefix, locale in LOCALE_MAP.items():
        doc_path = os.path.join(docs_dir, f"StoreListing_{prefix}.md")
        data = parse_markdown_listing(doc_path)
        if data:
            parsed_listings[locale] = data
            print(f"  Parsed {locale} listing from StoreListing_{prefix}.md ({len(data['features'])} features)")
    return parsed_listings


def update_single_listing(base_listing, new_data):
    fields = [
        ('Description', 'description'),
        ('ReleaseNotes', 'releaseNotes'),
        ('Features', 'features'),
        ('Keywords', 'keywords')
    ]
    for json_key, parsed_key in fields:
        val = new_data.get(parsed_key)
        if val:
            camel_key = json_key[0].lower() + json_key[1:]
            for k in (json_key, json_key.lower(), camel_key):
                base_listing.pop(k, None)
            base_listing[camel_key] = val[:7] if parsed_key == 'keywords' else val


def update_metadata(metadata, parsed_listings):
    listings = metadata.get('listings') or metadata.get('Listings') or {}
    if not isinstance(listings, dict):
        return 0

    updated_count = 0
    for lang, container in listings.items():
        base = container.get('baseListing') or container.get('BaseListing')
        if not isinstance(base, dict):
            continue

        matched_data = None
        for k, v in parsed_listings.items():
            if k.lower() == lang.lower() or (k.lower().startswith('en') and lang.lower().startswith('en')):
                matched_data = v
                break

        if matched_data:
            update_single_listing(base, matched_data)
            updated_count += 1
            print(f"  Updated metadata fields for locale: {lang}")
    return updated_count


def delete_pending_submission(token, p_id):
    print("Checking for existing pending submission...")
    if is_dry_run():
        return
    app_url = f'https://manage.devcenter.microsoft.com/v1.0/my/applications/{p_id}'
    app = api_request(app_url, token)
    if not app:
        return
    pending = app.get('pendingApplicationSubmission')
    if not pending:
        print("No pending submission found. Clean state verified.")
        return

    sub_id = pending.get('id')
    print(f"Found pending submission {sub_id}. Deleting...")
    del_url = f'https://manage.devcenter.microsoft.com/v1.0/my/applications/{p_id}/submissions/{sub_id}'
    api_request(del_url, token, method='DELETE')
    time.sleep(10)


def create_new_submission(token, p_id):
    print("Creating new submission draft in Partner Center...")
    if is_dry_run():
        return {"id": "MOCK_SUBMISSION", "fileUploadUrl": "https://example.com/mock_sas", "listings": {}}
    url = f'https://manage.devcenter.microsoft.com/v1.0/my/applications/{p_id}/submissions'
    result = api_request(url, token, method='POST', headers={'Content-Length': '0'}, retries=5, delay=20)
    if not result:
        print("Failed to create submission in Partner Center.")
        sys.exit(1)
    print(f"✅ Submission draft created: {result.get('id')}")
    return result


def find_screenshot_files(image_dir):
    if not os.path.isdir(image_dir):
        return {}
    return {
        fn: meta for fn, meta in IMAGE_FILE_MAP.items()
        if os.path.exists(os.path.join(image_dir, fn))
    }


def create_package_archive(msix_path, image_dir, screenshot_files):
    msix_name = os.path.basename(msix_path)
    if is_dry_run() and not os.path.exists(msix_path):
        print(f"  [DRY-RUN] Simulated archive of MSIX package: {msix_name}")
        return b"MOCK_ZIP_DATA"

    zip_buf = io.BytesIO()
    with zipfile.ZipFile(zip_buf, 'w', zipfile.ZIP_DEFLATED) as zf:
        zf.write(msix_path, msix_name)
        print(f"  Archived MSIX package: {msix_name}")
        for fn in screenshot_files:
            zf.write(os.path.join(image_dir, fn), fn)
            print(f"  Archived screenshot: {fn}")
    return zip_buf.getvalue()


def upload_package_archive(file_upload_url, zip_data):
    print(f"Uploading ZIP package archive ({len(zip_data)} bytes) to Azure Blob Storage...")
    if is_dry_run():
        print("[DRY-RUN] Upload simulation successful.")
        return
    sas_req = urllib.request.Request(
        file_upload_url,
        data=zip_data,
        headers={
            'x-ms-blob-type': 'BlockBlob',
            'Content-Type': 'application/zip',
            'Content-Length': str(len(zip_data))
        },
        method='PUT'
    )
    with open_https(sas_req, timeout=180) as resp:
        print(f"✅ Upload completed. Response code: {resp.status} {resp.reason}")


def update_package_refs(metadata, msix_name):
    packages = metadata.get(APP_PACKAGES_KEY) or metadata.get('applicationPackages') or []
    new_packages = [dict(pkg, fileStatus='PendingDelete') for pkg in packages]
    new_packages.append({'fileName': msix_name, 'fileStatus': 'PendingUpload'})
    metadata['applicationPackages'] = new_packages


def update_listing_image_refs(metadata, image_dir, screenshot_files):
    if not screenshot_files:
        return
    listings = metadata.get('listings') or metadata.get('Listings') or {}
    for lang, container in listings.items():
        base = container.get('baseListing') or container.get('BaseListing') or {}
        existing_images = base.get('images') or base.get('Images') or []
        new_for_lang = [
            {'fileName': fn, 'fileStatus': 'PendingUpload', 'imageType': meta[1]}
            for fn, meta in screenshot_files.items()
            if meta[0] == lang.lower() or (meta[0] == 'en-us' and lang.lower().startswith('en'))
        ]
        if not new_for_lang:
            continue
        new_names = {item['fileName'] for item in new_for_lang}
        for img in existing_images:
            if img.get('fileName') in new_names:
                img['fileStatus'] = 'PendingDelete'
        base['images'] = existing_images + new_for_lang


def sanitize_keywords_recursive(obj):
    if isinstance(obj, dict):
        for k, v in obj.items():
            if k.lower() == 'keywords' and isinstance(v, list):
                obj[k] = v[:7]
            else:
                sanitize_keywords_recursive(v)
    elif isinstance(obj, list):
        for item in obj:
            sanitize_keywords_recursive(item)


def wait_for_preprocessing(token, p_id, submission_id):
    print("\nWaiting for package upload acknowledgment...")
    if is_dry_run():
        return True
    sub_url = f'https://manage.devcenter.microsoft.com/v1.0/my/applications/{p_id}/submissions/{submission_id}'
    for check in range(1, 11):
        submission = api_request(sub_url, token, retries=3)
        if submission:
            status = submission.get('status') or submission.get('Status')
            print(f"  Preprocessing state ({check}/10): {status}")
            if status == 'PendingCommit':
                print("✅ Package verified and ready for commit.")
                return True
            if status == 'CommitFailed':
                print("❌ Submission validation failed in preprocessing.")
                return False
        time.sleep(15)
    return True


def commit_submission(token, p_id, submission_id):
    print("\nCommitting submission to Microsoft Store...")
    if is_dry_run():
        print("[DRY-RUN] Simulated commit successful.")
        return True
    commit_url = f'https://manage.devcenter.microsoft.com/v1.0/my/applications/{p_id}/submissions/{submission_id}/commit'
    result = api_request(commit_url, token, method='POST', headers={'Content-Length': '0'})
    return result is not None


def verify_commit_status(token, p_id, submission_id):
    print("\nVerifying submission commit status...")
    if is_dry_run():
        return 'confirmed'
    sub_url = f'https://manage.devcenter.microsoft.com/v1.0/my/applications/{p_id}/submissions/{submission_id}'
    for check in range(1, 10):
        submission = api_request(sub_url, token, retries=3, delay=10)
        if submission:
            status = submission.get('status') or submission.get('Status')
            print(f"  Post-commit status ({check}/9): {status}")
            if status in ACCEPTED_STATUSES:
                return 'confirmed'
            if status in IN_FLIGHT_STATUSES:
                print("  Commit in progress...")
            if status == 'CommitFailed':
                return None
        time.sleep(20)
    return 'unverified'


def run_submission_flow(repo_root, token, p_id, msix_path):
    delete_pending_submission(token, p_id)
    submission = create_new_submission(token, p_id)
    submission_id = submission['id']

    image_dir = os.path.join(repo_root, IMAGE_DIR_NAME)
    screenshot_files = find_screenshot_files(image_dir)
    zip_data = create_package_archive(msix_path, image_dir, screenshot_files)
    upload_package_archive(submission['fileUploadUrl'], zip_data)

    parsed_listings = parse_all_listings(repo_root)
    update_metadata(submission, parsed_listings)
    update_package_refs(submission, os.path.basename(msix_path))
    update_listing_image_refs(submission, image_dir, screenshot_files)
    sanitize_keywords_recursive(submission)

    sub_url = f'https://manage.devcenter.microsoft.com/v1.0/my/applications/{p_id}/submissions/{submission_id}'
    api_request(sub_url, token, method='PUT', body_dict=submission)

    if not wait_for_preprocessing(token, p_id, submission_id):
        print("❌ Preprocessing failed. Aborting.")
        delete_pending_submission(token, p_id)
        sys.exit(1)

    if not commit_submission(token, p_id, submission_id):
        print("❌ Failed to commit submission.")
        sys.exit(1)

    outcome = verify_commit_status(token, p_id, submission_id)
    if outcome in ('confirmed', 'unverified'):
        print(f"\n🎉 SUCCESS: Tapster submitted to Microsoft Partner Center! (Outcome: {outcome})")
    else:
        print("\n❌ Submission failed to reach confirmed state.")
        sys.exit(1)


def main():
    print("=" * 60)
    print("Tapster Microsoft Store Publishing Utility")
    print("=" * 60)

    if is_dry_run():
        print("[DRY-RUN] Running in simulation mode. No external changes will be made.")

    script_dir = os.path.dirname(os.path.abspath(__file__))
    repo_root = os.path.dirname(script_dir)
    config_path = os.path.join(script_dir, "local_store_config.json")
    t_id, c_id, c_sec, p_id, m_path = load_store_config(config_path)

    if not os.path.isabs(m_path):
        m_path = os.path.abspath(os.path.join(repo_root, m_path))

    token = get_token(t_id, c_id, c_sec)
    run_submission_flow(repo_root, token, p_id, m_path)
    return 0


if __name__ == '__main__':
    sys.exit(main())
