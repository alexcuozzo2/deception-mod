#!/usr/bin/env python3
"""Build against local libraries; never install/replace framework libraries.
No downloads, game launch, Finder activation or automatic report ZIPs.
"""
import argparse
import configparser
import contextlib
import datetime as dt
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import platform
import plistlib
import re
import shutil
import stat
import subprocess
import sys
import tempfile
import zipfile

ROOT=Path(__file__).resolve().parents[1]
_bounded_spec=importlib.util.spec_from_file_location('deception_bounded_process',ROOT/'tools/bounded_process.py')
_bounded=importlib.util.module_from_spec(_bounded_spec)
_bounded_spec.loader.exec_module(_bounded)
CONTRACT=json.loads((ROOT/'tools/host-contract.json').read_text())
GAME=Path.home()/'Library/Application Support/Steam/steamapps/common/The Battle of Polytopia'
MODNAME='alex_deception_m0.polymod'
PREVIOUS_MOD_SHA256='bb71e0434f449cc34cdc8a8a26ac02f4fb558b020cda8a6c03bd265dbcda54f5'
# Observed in Alex's uploaded M1A receipt; exact prior content, not an arbitrary overwrite.
OBSERVED_M1A_SHA256='57e1389e2d0ad1212a3776d23b5eeeaa55121566634996bb1d159e254873b7ee'
OBSERVED_M1B_SHA256='9ebbb99a905820a0665e34598ff182f88947f81790712703299eb9d5a282fa55'
# Exact archive hash in the successful M1C report supplied with this handoff.
OBSERVED_M1C_SHA256='cfdf6508d83e8f540d0f874c78d48a47bcd13fa94f1f858a83eefecce00ba13c'
# Exact successful R1 archive from the supplied e4cvmek6 Mac report.
OBSERVED_M1D_R1_SHA256='e0d19a3f903031f4108ac184514e9f94c32cef9331d53652dee4e09a132073d7'
# Exact installed M1E archive: environment/OBSERVED_M1E_LOCK.json and supplied
# be47lo_p report receipt; independently rehashed on this Mac 2026-09-15.
OBSERVED_M1E_SHA256='4d30c151870657ada9f005794ed57dc5d9fbce034f7bf161c834a259302d97da'
# Locally built/installed diagnostic receipt, project/evidence/observed-m1e-diagnostic-026.json.
OBSERVED_M1E_DIAGNOSTIC_026_SHA256='6447deb2679561632c72ad9a1ac02c395a0ec63892befbeaf646ada60b56836a'
# Locally built/installed repair receipt, project/evidence/observed-m1e-repair-027.json.
OBSERVED_M1E_REPAIR_027_SHA256='83acf8b86bb0ac6b1a0f620177d39ab01b3b4b94b5e1bbf9d7e347982d22023b'
# Exact locally installed 0.2.8, project/evidence/observed-m1e-repair-028.json.
OBSERVED_M1E_REPAIR_028_SHA256='929fa186a85bdffb85e2fda17e0b21a4cb23efc421e1590730bd32dad07ad2a6'
# Exact locally installed 0.2.9, project/evidence/observed-m1e-repair-029.json.
OBSERVED_M1E_REPAIR_029_SHA256='263fd3b2a6be1053ac83249ec143f01d274f52f1a3c3a05eb612735e2fac9bc2'
# Exact locally installed 0.2.10, project/evidence/observed-m1e-repair-0210.json.
OBSERVED_M1E_REPAIR_0210_SHA256='24de8ba0ae7199e7733389db243d41945cc3665516e71c630230a2b45ab8390e'
# Exact locally installed 0.2.11, project/evidence/observed-m1e-repair-0211.json.
OBSERVED_M1E_REPAIR_0211_SHA256='76bd89f30727a41991a35dd1f9320d94373470f6122fa4522f26b7e6abc9bd89'
# Exact local UI-A06 candidate; project/evidence/observed-session-map-0212.json.
OBSERVED_SESSION_MAP_0212_SHA256='509f0f939febf464cc9de9efa0b43b92f71d66102f2816537b5562a4133ba8d6'
# Exact observed UI-A07 candidate; project/evidence/observed-session-map-0213.json.
OBSERVED_SESSION_MAP_0213_SHA256='81497aaa7e83b6556cf8313cdf93b424f9525cc87bae7bd1e511ae5ffc83d0a0'
# Exact observed UI-A08 candidate; project/evidence/observed-session-map-0214.json.
OBSERVED_SESSION_MAP_0214_SHA256='0f3b338f9255becec665f00703c30dfb877ed41dcc927465c2fbcbb49d6f22dc'
# Exact observed UI-A09 candidate; project/evidence/observed-session-map-0215.json.
OBSERVED_SESSION_MAP_0215_SHA256='d0d07e852675aff0bd1f88b4efdc7d4ccc749cd1bf50b74bb94fbf5dad06d686'
# Exact observed UI-A10 candidate; project/evidence/observed-session-map-0216.json.
OBSERVED_SESSION_MAP_0216_SHA256='590747accc61f6380e387f69af6c7e3fb5ee85c5423f4f5e1834a3b85d855bb2'
# Observed native Smuggle candidate; evidence/observed-smuggle-030.json.
OBSERVED_SMUGGLE_030_SHA256='213f55975a70204c24ea6969a56d418ca89a02cd04b70ef286a0c9a0b2b49885'
# Observed native diagnostic; evidence/observed-smuggle-031.json.
OBSERVED_SMUGGLE_031_SHA256='1a8ce639b1df3f83edef28068c6ffae3ea5c796f40edc469653268b32ec5f9f8'
# Exact observed resource-lookup candidate; evidence/observed-smuggle-032.json.
OBSERVED_SMUGGLE_032_SHA256='fa5be8a92bdf8419b2fe8df91f512e9e9d149966c864dae73e459d12339e534a'
# Exact observed native Smuggle candidate; evidence/observed-smuggle-033.json.
OBSERVED_SMUGGLE_033_SHA256='832d10d7ba569196b00473314606a94671b02456584a15f2592fde9459577582'
# Exact observed native Smuggle lifecycle repair; evidence/observed-smuggle-034.json.
OBSERVED_SMUGGLE_034_SHA256='9584ec6a638747d325ecf596259e79a9406d16372ec85eba94027d228cd4fd09'
# Exact observed detached native probe candidate; evidence/observed-native-probe-035.json.
OBSERVED_NATIVE_PROBE_035_SHA256='339a8ebc40ded7158ec0afa34ffed7ab28b82583cedfe07b8f9198fee416173a'
# Exact observed detached envelope diagnostic; evidence/observed-native-envelope-036.json.
OBSERVED_NATIVE_ENVELOPE_036_SHA256='037a7a1f884c2300a662a9020a20230819d5cb85acd385220aa36be94805cecc'
# Exact native bridge acceptance candidate; evidence/observed-native-bridge-037.json.
OBSERVED_NATIVE_BRIDGE_037_SHA256='da01f126cf8b05ca8a5235dcff1278d153ff2e824e18dd18a14165815b9c5416'
# Exact native ledger acceptance candidate; evidence/observed-native-ledger-038.json.
OBSERVED_NATIVE_LEDGER_038_SHA256='c98cb4101f1875546b30f20436e0125b40998211fe75a702ba0b459a68a6ce9f'
# Exact observed command/recruitment candidate; evidence/observed-command-probe-039.json.
OBSERVED_COMMAND_PROBE_039_SHA256='9138549a7433661379cac788fc4f38a8433fe9cc3135b3d795d9c56a91523f8b'
# Exact observed command/style candidate; evidence/observed-command-probe-0310.json.
OBSERVED_COMMAND_PROBE_0310_SHA256='87ab82d33bda25f490fbc8f31d296064db8cfa5ac670a2ebf73f0b36346a2692'
# Exact observed command/registry diagnostic; evidence/observed-exchange-probe-0311.json.
OBSERVED_EXCHANGE_PROBE_0311_SHA256='bbd3def349b6e95ab2e5eb3db52a6cf697d3221199d8c41ef2db842b4da0268b'
# Exact observed stable registry and204native exchanges; evidence/observed-registry-exchange-0312.json.
OBSERVED_REGISTRY_EXCHANGE_0312_SHA256='1bce6c532d1a0f25bc2c36d6a42cb9bc62e58e68b03050396f2598faaf00e187'
# Exact native queued-sight trial; evidence/observed-exchange-sight-0313.json.
OBSERVED_EXCHANGE_SIGHT_0313_SHA256='40babbcf141213dc37202e8e39bb8a7dc71764afe15119f3d24b038d36a98a65'
# Exact native child-execution trial; evidence/observed-exchange-children-0314.json.
OBSERVED_EXCHANGE_CHILDREN_0314_SHA256='b7d26ed48277bda79db9498db8d03c8d608f0c6829e6c186addda2a1b657217c'
# Exact native ownership trial; evidence/observed-native-ownership-0315.json.
OBSERVED_NATIVE_OWNERSHIP_0315_SHA256='04e209b742140a7c7647c6f0138a04820867889b1a42030a52337a527b895ac7'
# Exact observed fresh-session diagnostic; evidence/observed-native-session-0316.json.
OBSERVED_NATIVE_SESSION_0316_SHA256='80d4a2731008adf106b41f39b14c767af44de99a94caae5e05d31f0868523997'
# Exact observed fresh-session and failure tests; evidence/observed-native-session-0317.json.
OBSERVED_NATIVE_SESSION_0317_SHA256='826090d1cab391fe7017ef4a3fd590b4c7f90c546289fed915f770cef97c7ea9'
# Exact installed execution diagnostic, whose trial did not start; evidence/observed-native-execution-0318.json.
OBSERVED_NATIVE_EXECUTION_0318_SHA256='7afebae2b3213795aaa3ffadca7cb61380b93aa899edc3410f056e9bfb2911ea'
# Exact installed readiness diagnostic; evidence/observed-native-execution-0319.json.
OBSERVED_NATIVE_EXECUTION_0319_SHA256='95e9974562dbfd386cf090bb00ed2396437fccb1bf3b02d1d25f299bc7387a8e'
# Exact installed first-copy diagnostic; evidence/observed-native-execution-0320.json.
OBSERVED_NATIVE_EXECUTION_0320_SHA256='f502ddd0406d78994a358d5a61c4378452723139337fa5a0c3ac7b425d1b488c'
# Exact installed native-copy diagnostic; evidence/observed-native-execution-0321.json.
OBSERVED_NATIVE_EXECUTION_0321_SHA256='b98eea64c21573553e9a067e87109733048b3a857436c76dc83e521cad163ded'
# Exact installed two-command diagnostic; evidence/observed-native-execution-0322.json.
OBSERVED_NATIVE_EXECUTION_0322_SHA256='7ded2df326f17556b6d6eed0f8a5a0db0d1f32702011de9c026bde260361eb75'
# Exact native137 and ordinary-play verified archive; evidence/observed-native-execution-0323.json.
OBSERVED_NATIVE_EXECUTION_0323_SHA256='78cd7a504a13e1c40b79692ba7f0d8b13dd07719c3464c3c70ca38249b3b66e1'
# Exact installed private paid-birth candidate; evidence/observed-native-paid-birth-0324.json.
OBSERVED_NATIVE_PAID_BIRTH_0324_SHA256='3b42cc5a3fe06ada6fdcd2c0c2d135fc2f2e90ba50b4506d38b5e30fcbc41a0a'
# Exact observed A11 candidate stopped at startup for shifted IDs; evidence/observed-a11-0325.json.
OBSERVED_A11_0325_SHA256='126843e8bfd3d7a020dbbe0e691bbb72f2b904331267c67de9ada115e8c811fc'
# Exact installed A11 stable-registry candidate; native Propaganda reaction failed.
# Provenance: evidence/observed-a11-0326.json.
OBSERVED_A11_0326_SHA256='06c71ffc0d4678ad3952f103ec8938f2e54f52f4d149f3ce4acc303c5a2f709b'
# Exact observed native research/Propaganda candidate; evidence/observed-a11-0327.json.
OBSERVED_A11_0327_SHA256='dcc05c6fe83723a7ce00ea29b77f39b9594b81771f2fa9342535deae2efa3bff'
# Exact observed selection-repair candidate; evidence/observed-a11-0328.json.
OBSERVED_A11_0328_SHA256='fd72d00aab12426819766f210d204293ab4764464b362e91425d8caa3fceff91'
# Exact observed visible-overlay candidate; evidence/observed-a11-0329.json.
OBSERVED_A11_0329_SHA256='5395ccc813aa6a1c08c805db6303207afe42a3c246ac24f0994809b56a6f27a6'
# Observed standalone Train probe before native install; evidence/observed-train-0330.json.
OBSERVED_TRAIN_0330_SHA256='3e9bb7ce486958cbbe195f5bfeaa041362a327a2b8c0fb4334bb52d2d6a38c98'
# Exact observed native census-fix candidate; evidence/observed-train-0331.json.
OBSERVED_TRAIN_0331_SHA256='eb751f57f11f6c50f9fb1007f4032c74d88be6c07e2811c47dd9cbf2bab7ac70'
# Exact native leaf-pass candidate; evidence/observed-train-0332.json.
OBSERVED_TRAIN_0332_SHA256='06e519d462a034ee0e3122e62f34b7efbe3d77857801f28e9f2682c835eea3f2'
# Exact native leaf/factory pass; evidence/observed-train-0333.json.
OBSERVED_TRAIN_0333_SHA256='5434013e61d33cac99627a6dfbcafc396da99fe8b04ac396a638c89dc753cde1'
# Exact typed outer read-ack failure; evidence/observed-command-0334.json.
OBSERVED_COMMAND_0334_SHA256='2684a26e66ee751e6f707eb938acb81f0515e93a189469278b97e7bd3250b806'
# Exact native return-version diagnostic; evidence/observed-command-0335.json.
OBSERVED_COMMAND_0335_SHA256='339805c1fa41ecf6a4f1ef519817b02d0d9e4e970f5b6336cab44a032700c05b'
# Exact observed 0.3.36 installed archive; provenance in evidence/observed-command-0336.json.
OBSERVED_COMMAND_0336_SHA256='53c1c58139c5e4f7406f741c6411b574fd20b16c2ae6b079787ebfc97a534dbb'
# Exact observed 0.3.37 archive; provenance in evidence/observed-build-0337.json.
OBSERVED_BUILD_0337_SHA256='659297fecdae53e4efe4c8047ca99699e3e6859e068eb40879a403d8d8ea3af7'
# Exact observed 0.3.38 archive; evidence/observed-build-factory-0338.json.
OBSERVED_BUILD_FACTORY_0338_SHA256='0f9b4b22748080ddf540996ad669179f96985672d831c90c33ea38dd63154bab'
# Exact installed and Alex-tested 0.3.39; evidence/observed-art-baseline-0339.json.
OBSERVED_ART_BASELINE_0339_SHA256='2065b8f827c9bb9f32db00d1a0f5e147b8186f3b6d21ad8e51c7ed7f19de6d30'
# Exact Alex-tested artwork integration; evidence/observed-art-0340.json.
OBSERVED_ART_0340_SHA256='9911393de1567783a4298d4e886fd128d0fa8c06f39ec629751782aa48355f02'
# Exact Alex-tested preview-repair baseline; evidence/observed-art-0341.json.
OBSERVED_ART_0341_SHA256='6e02a3f1c0fd5e84e44cbc829525b84f93df41097e5afeaac4f3fc4cda45039e'
# Exact Alex-tested art baseline; evidence/observed-art-0342.json.
OBSERVED_ART_0342_SHA256='2b4d77c673cf9dd7b678cb005c65b2e6552b100ffb5b5af9b2875d658b89b322'
# Exact Alex-tested 0.3.43 archive, verified after save restoration; evidence/observed-art-0343.json.
OBSERVED_ART_0343_SHA256='6688f9dd249bfca2c1663f28f05fa923475e6a991929f6109feb43f54bda97c3'
# Exact Alex-tested 0.3.44 archive after protected-save restoration; evidence/observed-art-0344.json.
OBSERVED_ART_0344_SHA256='f290cf2333044109b0c879664091cef23922ae1013c2e33fb5d843ad8957f73d'
# Exact Alex-tested0.3.45 archive after confirmed test archival/current-save restoration; evidence/observed-art-0345.json.
OBSERVED_ART_0345_SHA256='c73d7a52939e421adfeaf0fb0eabdc9efe7d54244c236f5388060f7b8db97b40'
# Exact Alex-tested 0.3.46 archive after enrolled test archival and current-save restoration; evidence/observed-art-0346.json.
OBSERVED_ART_0346_SHA256='9311bd007e305098bf4b9c3ee1f86aad4d01f879c36180d2c089458ec29778b2'
# Exact Alex-tested 0.3.47 archive after enrolled test archival and current-save restoration; evidence/observed-art-0347.json.
OBSERVED_ART_0347_SHA256='7f3eb2018080fab9116fd78294b128aab84e27baf787c75b4ed033244e931eb6'
# Exact Alex-tested 0.3.48 archive after enrolled test archival and current-save restoration; evidence/observed-art-0348.json.
OBSERVED_ART_0348_SHA256='462b8e71e041ccfbaa46cc2aa6618bc2408cf2e15082ea22115407dc2bc0fdea'
# Exact installed 0.3.49 verified before this increment; evidence/observed-art-0349.json.
OBSERVED_ART_0349_SHA256='c8a6a21e4618092d291e56f2e46e2dc217b63c340bd8098c809806b15a6231ab'
# Exact user-tested 0.3.50; evidence/observed-regroup-0350.json.
OBSERVED_REGROUP_0350_SHA256='df2d55d60b715cae9a89cce7a1cdc9d150bea9dd175c69005d9ec9c6d0e20337'
# Exact installed 0.3.51; evidence/observed-regroup-0351.json.
OBSERVED_REGROUP_0351_SHA256='8d421e2579d9b5a296ff8f070d61fdde8cdb030c6f15e84eee910166714ae8c4'
# Exact installed 0.3.52; evidence/observed-regroup-0352.json.
OBSERVED_REGROUP_0352_SHA256='c006b31db7aaccb1bfdf6030af35ec93fa3276a54757f5201b561f90c5c02bb5'
# Exact installed 0.3.53; evidence/observed-regroup-0353.json.
OBSERVED_REGROUP_0353_SHA256='cd8249d02c1a26e1fbfc5d22a914ee476848ce8713e9c46b087ada967aa252cd'
# Exact Alex-tested 0.3.54; evidence/observed-regroup-0354.json.
OBSERVED_REGROUP_0354_SHA256='63f4ef6941a67a748ff808122444823f29bd6051515a930476f21a1561bbdd84'
# Exact Alex-tested growing-disguise build; evidence/observed-growing-0355.json.
OBSERVED_GROWING_0355_SHA256='5da9d10ba79840c5396b1f53f685676be0b1c64f9534567147a2341840abb28d'
# Exact installed 0.3.56; evidence/observed-icons-0356.json.
OBSERVED_ICONS_0356_SHA256='65aa9254ed8327b45f8102ecdd8000bfd12e1479c4f67f852f102b9c25c88386'
# Exact Alex-tested0.3.57 naval build; evidence/observed-naval-0357.json.
OBSERVED_NAVAL_0357_SHA256='cc4c10c5cc4b2fa2650c0c82b32c84a6ffb9432e13dbfac34d6164bd70ef0146'
# Exact Alex-tested 0.3.58; evidence/observed-feedback-0358.json.
OBSERVED_FEEDBACK_0358_SHA256='75a062ac2e634eaa45d4f9e142aa4be3031b47c69603aed81425e02e1f587cfe'
# Exact crashed installed0.3.59; evidence/observed-crash-0359.json.
OBSERVED_CRASH_0359_SHA256='05bd887b5ae76f7ac4201c8be32c23eb137de3e3dead525c3be5128606af3a45'
# Exact Alex-tested0.3.60; evidence/observed-repair-0360.json.
OBSERVED_REPAIR_0360_SHA256='c4cf9b80a1b59ca204e3b2c723bed91dbbad8bf3ec854256d24f00615ea36ee5'
# Exact Alex-tested0.3.61; evidence/observed-spy-0361.json.
OBSERVED_SPY_0361_SHA256='a3635675d2a02c166455c5838104825d8985aa0fa84c614cd11496fb5e1c8e71'
# Exact Alex-tested 0.3.62; evidence/observed-espionage-0362.json.
OBSERVED_ESPIONAGE_0362_SHA256='2bb733ae574100f5cde2449367733e4fff214319869eff7284a26b741baa0e99'
# Exact installed Hotseat/Cloak candidate; evidence/observed-hotseat-0363.json.
OBSERVED_HOTSEAT_0363_SHA256='804fe49948d2de1eb630c8cd82f5b1658c5e0e48ab7c2243c112873377666196'
# Exact installed Alex-tested0.3.64; evidence/observed-startup-0364.json.
OBSERVED_STARTUP_0364_SHA256='0f192f7c58d421e11d11e51fae09b16cb08be06e28a2dbfa3717200586d3a68a'
# Exact installed Alex-tested0.3.65; evidence/observed-support-0365.json.
OBSERVED_SUPPORT_0365_SHA256='d5fccd6067b19195dffc3692f9fadbef761a9dad574e9a7310f3e67f977b4720'
# Exact installed Alex-tested0.3.66; evidence/observed-observer-0366.json.
OBSERVED_OBSERVER_0366_SHA256='bb4988478856fc59b7aea8f86e223780b9c54f136438796fe450c97d3433aeb4'
# Exact installed0.3.67 archive; evidence/observed-blackmail-base-0367.json.
OBSERVED_BLACKMAIL_BASE_0367_SHA256='7a40f20cd89f58839ffa5549c37dd0ed3757f02d0acaeef0bff80211a559e263'
# Exact Alex-tested0.3.68 archive; evidence/observed-blackmail-ui-0368.json.
OBSERVED_BLACKMAIL_UI_0368_SHA256='96a9817723df3fac7dec3a9dad165da52f2e1497098e3c1f9f45cd72ad851174'
# Exact Alex-tested0.3.69 archive; evidence/observed-spy-feedback-0369.json.
OBSERVED_SPY_FEEDBACK_0369_SHA256='b5c825ea0d0377894aa1a60b80168e2e853f4f7f76deb9e7bcad753299acc223'
# Exact installed0.3.70 baseline; evidence/observed-aquarion-0370.json.
OBSERVED_AQUARION_0370_SHA256='09f232ede0d60b6930de4e68270511a7b5fcccb4f3191e03e5a2e27f18bfa3f6'
# Exact installed0.3.71, copied before repair; evidence/observed-aquarion-repair-0371.json.
OBSERVED_AQUARION_REPAIR_0371_SHA256='8cd94961966cb77a3c1e123e4b346a784b791845d89ef87ff6b024127c74a1d8'
# Exact installed0.3.73 captured before0.3.74 edits; evidence/observed-spy-disguise-0373.json.
OBSERVED_SPY_DISGUISE_0373_SHA256='b2e23a8ea20d2b56d151224b574ab0a69f181ab803fc2ede3885e6a479c8a72b'
# Exact installed0.3.74 captured before0.3.75 edits; evidence/observed-save-fog-0374.json.
OBSERVED_SAVE_FOG_0374_SHA256='fdf77e3fb0de53bd2eccae79eb7ab2df9f9a40f415b4fe55d0b3da4110c1bb77'
# Exact installed0.3.75 captured before0.3.76 edits; evidence/observed-preview-loading-0375.json.
OBSERVED_PREVIEW_LOADING_0375_SHA256='84feaee19c79f2f6d9ea36f8ac90e2f558f94cd05d46413585ec2f18418e5f38'
# Exact installed0.3.76 before0.3.77 edits; evidence/observed-spy-actions-0376.json.
OBSERVED_SPY_ACTIONS_0376_SHA256='a26648a537dfedc7a797c93253e278db6600d6177ce60d92e2e2b2926f832501'
# Exact installed 0.3.77 before Rogue crash/portability repairs; evidence/observed-rogue-crash-0377.json.
OBSERVED_ROGUE_CRASH_0377_SHA256='c40d02bc6ce3a030c6ffe77aea44dc0be63a23381224d660d9e7999e15da8be8'
# Exact installed Alex-tested0.3.78; evidence/observed-native-focus-0378.json.
OBSERVED_NATIVE_FOCUS_0378_SHA256='e4a8392c1a88ecefc35c36c9f8617aed23b5bc7fad755b1801979bce410f0f7a'
# Exact installed Alex-tested0.3.79; evidence/observed-cloak-0379.json.
OBSERVED_CLOAK_0379_SHA256='0a3f00bd610e357a955d6aab75472e5fe08ee47b4118ae9432d95c4329801bba'
# Exact installed Alex-tested0.3.80; evidence/observed-badge-0380.json.
OBSERVED_BADGE_0380_SHA256='3c6ed1dae000e2a7991e44068432b414f57a62c6c36fcc25953d7deeaab7f30f'
# Exact installed0.3.81; evidence/observed-spy-balance-0381.json.
OBSERVED_SPY_BALANCE_0381_SHA256='44254b6c62f3f69ad10591531cc502c197c3335c79806523b30810ffc81fa8d8'
# Exact installed 0.3.82; evidence/observed-online-baseline-0382.json.
OBSERVED_ONLINE_BASELINE_0382_SHA256='bb86a22591603a401b09bdb6663766087a5d3daa908b8d5e5a7b0abfbbbd0f1e'
# Exact installed 0.3.83; evidence/observed-blackmail-layout-0383.json.
OBSERVED_BLACKMAIL_LAYOUT_0383_SHA256='19a6fed560841a5ddb41f2fae7a45d2b360d2b09ca2c98489c43c88819b0b5f9'
# Exact installed 0.3.84; evidence/observed-blackmail-relay-0384.json.
OBSERVED_BLACKMAIL_RELAY_0384_SHA256='8deef6c0c87deb854b82228e5c6290cb5da5078fcf96c11ba6941cb36e97a08e'
# Exact installed0.3.85; evidence/observed-unit-blackmail-0385.json and preserved baseline.
OBSERVED_UNIT_BLACKMAIL_0385_SHA256='409672abeda083c52c2b73a3e6d3fda110313e2291af04fd6787a4ee036aecfe'
# Exact installed predecessor; evidence/observed-smuggle-sanctuary-0386.json.
OBSERVED_SMUGGLE_SANCTUARY_0386_SHA256='770fb3a854f4414cf9f7b086645a08586a8389359e87df502d3bd769f1e808e7'
# Exact installed predecessor; evidence/observed-blackmail-cost-0387.json.
OBSERVED_BLACKMAIL_COST_0387_SHA256='b5fe88accdd4ef7b4d1fcdf04a3f4a543b74309e794c63232d972e9d5f16d9d3'
# Exact installed predecessor; evidence/observed-spy-stealth-0388.json.
OBSERVED_SPY_STEALTH_0388_SHA256='6bfa84c4a887ccc51de07274a9404039e6b0eddae0d54b7260a5408c60a62fad'
OBSERVED_SPYMAID_FAIRY_0372_SHA256='40ac06184432fcd7e8ae50cc0ed8df8bcce2238ef0519405cd8747ce7e4eed9e'
MILESTONE='M1E'
# Exact installed predecessor; evidence/observed-counterintelligence-0389.json.
OBSERVED_COUNTERINTELLIGENCE_0389_SHA256='05bbc05a2a8075ec36ce0d508e223bd0c7d2ea29656224f54e4cd0556e017ae7'
# Read-only installed receipt evidence: observed-art-updater-0390.json.
OBSERVED_ART_UPDATER_0390_SHA256='20f31e93ef2bf8b39be968167048097ea47f9554629962fc7b3781dace0d3abc'

# Exact installed predecessor; evidence/observed-feedback-0391.json.
OBSERVED_FEEDBACK_0391_SHA256='a49adab8909248cf59547a12b8e5a479af627dbfe2eb4fdfb1046cc67e5396dc'

# Exact installed predecessor; evidence/observed-trade-secrets-0392.json.
OBSERVED_TRADE_SECRETS_0392_SHA256='7503d309457f74895844705fa8d84c574e2cc140fad778d760f06c9d4eb83d3f'

# Exact installed predecessor; evidence/observed-flat-blackmail-0393.json.
OBSERVED_FLAT_BLACKMAIL_0393_SHA256='84b60481dc2d00e3c2cc523fcc5d20974721927ed6ba78f40ac1df0d84eb78cf'

# Exact installed predecessor; evidence/observed-fogger-badge-0394.json.
OBSERVED_FOGGER_BADGE_0394_SHA256='25a211981fe62c075fe07fbc40e0e903299388b5aa3a80f2c203d0180378057e'

# Exact installed predecessor; evidence/observed-resign-crash-0395.json.
OBSERVED_RESIGN_CRASH_0395_SHA256='24f7760ecd84f156e4caa1abe6ec8638b963171a707ef545d6fad4f3fa54e2c2'

# Exact installed predecessor; evidence/observed-reload-safety-0396.json.
OBSERVED_RELOAD_SAFETY_0396_SHA256='0e8a5f282bddabaa6c280f2409196c8d73b0dca8deec5cc4071e3fa317266ca4'
# Exact observed 0.3.97; evidence/observed-blackout-discovery-0397.json.
OBSERVED_BLACKOUT_DISCOVERY_0397_SHA256='c68b2abc773139ad2b45ef8e66b49bdecaeab7f4e10c727b67f2edcf9f849692'
# Exact observed 0.3.98; evidence/observed-lighthouse-secret-0398.json.
OBSERVED_LIGHTHOUSE_SECRET_0398_SHA256='2b6953724987361bea6b7699b9e51690279c9e51d2fc074505c1a98dfacf820c'
# Exact observed 0.3.99; evidence/observed-spy-movement-0399.json.
OBSERVED_SPY_MOVEMENT_0399_SHA256='b8415fd93169393ac96d849b02cbbe11d7738661f648674f69b7088bcc3dfce6'
# Exact installed 0.3.100; run-output/lighthouse-removal-03100/release/installed-verification.json.
OBSERVED_LIGHTHOUSE_REMOVAL_03100_SHA256='98211f2a80934a5c16ebe1098a83cc91c2ca622ec56aaf4f918995640119c9cc'
# Exact installed 0.3.101, rehashed 2026-10-18; evidence/observed-branch-position-03101.json.
OBSERVED_BRANCH_POSITION_03101_SHA256='78db51e7e2b9dd0eff0016e60d4783223967dd47ff43c3d4e491aac1e10225e7'

UI_ADAPTER_SOURCES=('ModContentAssets.cs','NativeApi.cs','RecruitmentWheelUi.cs','TechPresentation.cs','NativeBranchTechUi.cs','BranchArtAssets.cs','FoggerArtAssets.cs','NativeFoggerPresentation.cs','TradeSecretsArtAssets.cs','BlackmailArtAssets.cs','NativeBlackmailUnitPresentation.cs','SmokeGrenadeAssets.cs','NativeSurveillanceUi.cs','NativeRogueVisuals.cs','TechIconGlow.cs','NativeButtonArt.cs','NativeVisualSnapshot.cs',
                    'NativeAquarionUnits.cs','MimicPresentationState.cs','NativeSpyPresentation.cs','NativeSpyActions.cs','MimicPresentationUi.cs','MimicControls.cs','NativeSpecialActionUi.cs','NativeActionBarScroll.cs','MimicActionMenu.cs','MimicAbilityTags.cs',
                    'MimicInspectionControls.cs','MimicDualPortrait.cs','MimicMapInspection.cs','NativeRoguePresentation.cs','MimicPortraitAsset.cs','UnitArtAssets.cs','UnitArtOutline.cs','MimicHoldInput.cs','OrdinaryActionEligibility.cs','MimicSmuggleActions.cs','MimicSmuggleUi.cs')

class Stop(RuntimeError): pass

def digest(data): return hashlib.sha256(data).hexdigest()
def sha(path):
    h=hashlib.sha256()
    with open(path,'rb') as f:
        for data in iter(lambda:f.read(1048576),b''):h.update(data)
    return h.hexdigest()
def regular(path):
    if not path.exists() or not stat.S_ISREG(path.lstat().st_mode):raise Stop('Expected an ordinary file: '+str(path))
    return path

def same_tree_sources():
    selected=[]
    for directory in ('src','tests','tools','content','spec'):
        for p in (ROOT/directory).rglob('*'):
            if p.is_file() and '__pycache__' not in p.parts and p.suffix!='.pyc':selected.append(p)
    selected.append(ROOT/'run.sh')
    if (ROOT/'run.ps1').is_file():selected.append(ROOT/'run.ps1')
    return {p.relative_to(ROOT).as_posix():sha(p) for p in sorted(selected)}

def clean_env():
    env=os.environ.copy()
    for k in list(env):
        if k.startswith(('DYLD_','DOORSTOP_','MONOMOD_')) or k in ('LD_PRELOAD','LD_LIBRARY_PATH','BEPINEX_GAME_ASSEMBLY_PATH','ARCHPREFERENCE'):
            env.pop(k,None)
    env['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
    env['DOTNET_SKIP_FIRST_TIME_EXPERIENCE']='1'
    return env

def call(args,log=None,env=None,cwd=None):
    if log:log.write('$ '+repr([str(a) for a in args])+'\n');log.flush()
    try:
        p=_bounded.run([str(a) for a in args],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,
                       text=True,env=env or clean_env(),cwd=cwd)
    except subprocess.TimeoutExpired as e:
        raise Stop('Build/test command exceeded 180 seconds; its process group was stopped: '+str(args[0])) from e
    if log:log.write(p.stdout);log.flush()
    if p.returncode:raise Stop('Command failed (exit %d): %s\n%s'%(p.returncode,args[0],p.stdout[-12000:]))
    return p.stdout

def preflight(game,closed=True):
    if platform.system()!='Darwin':raise Stop('This native build/install targets Alex\'s macOS installation. No installation changes made.')
    if closed:
        r=subprocess.run(['/usr/bin/pgrep','-u',str(os.getuid()),'-x','Polytopia'],stdout=subprocess.DEVNULL,stderr=subprocess.PIPE)
        if r.returncode==0:raise Stop('Quit Polytopia normally first. Steam may remain open.')
        if r.returncode!=1:raise Stop('Could not determine whether Polytopia is closed.')
    info=plistlib.loads(regular(game/'Polytopia.app/Contents/Info.plist').read_bytes())
    if info.get('CFBundleShortVersionString')!=CONTRACT['game_version']:raise Stop('Game version changed; refusing guessed API compatibility.')
    before={}
    for rel,expected in CONTRACT['framework_checksums'].items():
        actual=sha(regular(game/rel));before[rel]=actual
        if actual!=expected:raise Stop('Working compatibility library differs: '+rel+'. Do not restore/reinstall; inspect the difference.')
    harmony=game/'BepInEx/core/0Harmony.dll'
    if sha(regular(harmony))!=CONTRACT['reference_api_sha256']['0Harmony.dll']:raise Stop('Harmony API binary differs from the inspected version.')
    cfg=game/'BepInEx/config/BepInEx.cfg';config=configparser.ConfigParser(interpolation=None);config.optionxform=str
    config.read_string(regular(cfg).read_text(encoding='utf-8-sig'))
    for section,key,value in [('IL2CPP','ScanMethodRefs','false'),('Detours','DetourProviderType','Funchook'),('Logging','UnityLogListening','false')]:
        if config.get(section,key,fallback='').lower()!=value.lower():raise Stop('Required existing setting differs: '+section+'/'+key)
    # This checkpoint adds native wrapper calls; pin the exact APIs inspected.
    for name in ('GameLogicAssembly.dll','PolytopiaAssembly.dll','Il2Cppmscorlib.dll'):
        rel='BepInEx/interop/'+name
        if sha(regular(game/rel))!=CONTRACT['reference_api_sha256'][name]:
            raise Stop('Generated native API wrapper differs: '+name+'. Do not rebuild the framework; inspect first.')
        before[rel]=sha(game/rel)
    before['BepInEx/config/BepInEx.cfg']=sha(cfg)
    # Existing generated Unity dependency: inspect it locally at build time and
    # require byte identity at install. The handoff did not include these bytes;
    # no historic expected hash is invented and no library is replaced.
    rel='BepInEx/interop/UnityEngine.CoreModule.dll'
    before[rel]=sha(regular(game/rel))
    rel='BepInEx/interop/UnityEngine.ImageConversionModule.dll'
    before[rel]=sha(regular(game/rel))
    return before

def sdk():
    candidates=[shutil.which('dotnet'),'/usr/local/share/dotnet/dotnet','/opt/homebrew/opt/dotnet/libexec/dotnet',str(Path.home()/'.dotnet/dotnet')]
    for item in candidates:
        if not item or not Path(item).is_file():continue
        try:lines=call([item,'--list-sdks']).splitlines()
        except Stop:continue
        sdks=[]
        for line in lines:
            m=re.fullmatch(r'(\S+)\s+\[(.+)\]',line.strip())
            if m:
                compiler=Path(m.group(2))/m.group(1)/'Roslyn/bincore/csc.dll'
                if compiler.is_file():sdks.append((tuple(int(x) for x in re.findall(r'\d+',m.group(1))),compiler))
        if sdks:return Path(item),sorted(sdks)[-1][1]
    raise Stop('Existing .NET SDK compiler was not found. No SDK/runtime download or upgrade was attempted.')

def csc(dotnet,compiler,sources,out,refs,log,resources=(),unsafe=False,runner=None,defines=()):
    # Fixture-only data loading. Production reads patch.json from PolyMod's mod files.
    if out.name != 'Deception.dll' and any(p.name == 'ContentDefinitions.cs' for p in sources):
        resources=list(resources)+[(ROOT/'content/patch.json','Deception.Content.patch.json')]
        defines=tuple(defines)+('DECEPTION_CONTENT_FIXTURE',)
    if any(p.name == 'ModContentAssets.cs' for p in sources) and out.name != 'Deception.dll':
        sources=list(sources)+[ROOT/'tests/loose-content/RegistryFixture.cs']
    # Native runtime libraries are references only; nothing is copied into Mods/core.
    # Direct csc avoids NuGet restore and dependency/runtime upgrades.
    lines=['-nologo','-nostdlib+','-target:library','-langversion:10','-nullable:enable','-deterministic+','-optimize+','-warn:4']
    if unsafe:lines.append('-unsafe+')
    for define in defines:
        if not re.fullmatch(r'[A-Z][A-Z0-9_]*',define):raise Stop('Invalid compiler definition.')
        lines.append('-define:'+define)
    def quoted(x):
        s=str(x)
        if any(ch in s for ch in '\r\n"'):raise Stop('Unsupported compiler path characters.')
        return '"'+s+'"'
    lines.append('-out:'+quoted(out))
    lines.extend('-r:'+quoted(p) for p in refs)
    lines.extend('-resource:'+quoted(p)+','+name for p,name in resources)
    lines.extend(quoted(p) for p in sources)
    rsp=out.with_suffix('.rsp');rsp.write_text('\n'.join(lines)+'\n')
    (runner or call)([dotnet,'exec',compiler,'-noconfig','@'+str(rsp)],log=log)
    regular(out)

def content_entries():
    """Public, inspectable content shared by every platform's archive builder."""
    entries={'patch.json':regular(ROOT/'content/patch.json').read_bytes()}
    entries['assets/dcp_mimic-portrait__.png']=regular(ROOT/'content/mimic-portrait.png').read_bytes()
    for folder in ('unit-art','spy-effects'):
        for path in sorted((ROOT/'content'/folder).iterdir()):
            if path.suffix in ('.png','.wav'):
                name=('dcp_'+path.stem+'__.png') if path.suffix=='.png' else path.name
                entries['assets/'+folder+'/'+name]=regular(path).read_bytes()
    return entries

def make_archive(dll,destination):
    entries={n:(ROOT/'content'/n).read_bytes() for n in ('manifest.json','localization.json')}
    entries.update(content_entries())
    entries['Deception.dll']=regular(dll).read_bytes()
    entries['DEVELOPMENT-STATUS.txt']=(ROOT/'content/DEVELOPMENT-STATUS.txt').read_bytes()
    with zipfile.ZipFile(destination,'x',compression=zipfile.ZIP_DEFLATED) as z:
        for name,data in entries.items():
            i=zipfile.ZipInfo(name,(2026,9,14,0,0,0));i.compress_type=zipfile.ZIP_DEFLATED;i.external_attr=0o100644<<16;z.writestr(i,data)
    with zipfile.ZipFile(destination) as z:
        if z.testzip() is not None:raise Stop('Generated mod ZIP failed integrity check.')
        if set(z.namelist())!=set(entries):raise Stop('Unexpected files in generated content mod.')

def expected_model_ids():
    return json.loads(regular(ROOT/'tests/expected-model-tests.json').read_text())['ids']

def validate_model_results(results):
    expected=expected_model_ids();n=len(expected);tests=results.get('tests',[])
    if results.get('total')!=n or results.get('failed')!=0 or results.get('passed')!=n or results.get('is64Bit') is not True:
        raise Stop('The required %d managed model/registry tests did not all pass.'%n)
    if not isinstance(tests,list) or len(tests)!=n or any(not isinstance(t,dict) for t in tests):
        raise Stop('Managed model-test records are incomplete or inconsistent.')
    ids=[t.get('id') for t in tests]
    if any(not isinstance(i,str) for i in ids) or set(ids)!=set(expected) or len(set(ids))!=n:
        raise Stop('Managed test IDs differ from the required manifest.')
    if any(t.get('status')!='PASS' for t in tests):
        raise Stop('Managed model-test records contain a failure.')


def expected_ui_adapter_ids():
    return json.loads(regular(ROOT/'tests/expected-ui-adapter-tests.json').read_text())['ids']

def validate_ui_adapter_results(results):
    expected=expected_ui_adapter_ids();n=len(expected);tests=results.get('tests',[])
    if results.get('total')!=n or results.get('failed')!=0 or results.get('passed')!=n or results.get('is64Bit') is not True:
        raise Stop('The required %d managed UI-adapter fixture checks did not all pass.'%n)
    if not isinstance(tests,list) or len(tests)!=n or any(not isinstance(t,dict) for t in tests):
        raise Stop('UI-adapter test records are missing or inconsistent.')
    ids=[t.get('id') for t in tests]
    if any(not isinstance(i,str) for i in ids) or set(ids)!=set(expected) or len(set(ids))!=n or any(t.get('status')!='PASS' for t in tests):
        raise Stop('UI-adapter test IDs/status differ from the required manifest.')

def validate_train_leaf_results(name,results):
    expected=json.loads(regular(ROOT/'tests/train-leaf/expected.json').read_text())[name]
    rows=results.get('results',[])
    if results.get('failed')!=0 or len(rows)!=len(expected) or {r.get('id') for r in rows}!=set(expected) or any(r.get('status')!='PASS' for r in rows):
        raise Stop('Private Train leaf fixture IDs/status differ: '+name)

def validate_train_outer_results(name,results):
    expected=json.loads(regular(ROOT/'tests/train-outer/expected.json').read_text())[name]
    rows=results.get('results',[])
    if results.get('failed')!=0 or len(rows)!=len(expected) or {r.get('id') for r in rows}!=set(expected) or any(r.get('status')!='PASS' for r in rows):
        raise Stop('Private Train outer fixture IDs/status differ: '+name)


def validate_command_outer_results(name,results):
    expected=json.loads(regular(ROOT/'tests/command-outer/expected.json').read_text())[name]
    rows=results.get('results',[])
    if results.get('failed')!=0 or len(rows)!=len(expected) or {r.get('id') for r in rows}!=set(expected) or any(r.get('status')!='PASS' for r in rows):
        raise Stop('Typed command outer fixture IDs/status differ: '+name)

def validate_build_journal_results(name,results):
    expected=json.loads(regular(ROOT/'tests/build-journal/expected.json').read_text())[name]
    rows=results.get('results',[])
    if results.get('failed')!=0 or results.get('passed',len(rows))!=len(expected) or len(rows)!=len(expected) or {r.get('id') for r in rows}!=set(expected) or any(r.get('status')!='PASS' for r in rows):
        raise Stop('Build journal/adapter fixture IDs/status differ: '+name)

def validate_build_factory_results(name,results):
    expected=json.loads(regular(ROOT/'tests/build-factory/expected.json').read_text())[name]
    rows=results.get('results',[])
    if results.get('failed')!=0 or results.get('passed')!=len(expected) or len(rows)!=len(expected) or {r.get('id') for r in rows}!=set(expected) or any(r.get('status')!='PASS' for r in rows):
        raise Stop('Build factory fixture IDs/status differ: '+name)

def build_factory_sources(work):
    # Use the journal suite's exact extracted production store and native CLR
    # object fixture; include the actual Train/Build adapters and observer route.
    # Do not include the constructor-only suite's inactive traversal fixture.
    return sorted((ROOT/'src/Core').glob('*.cs'))+[ROOT/'src/PolyMod'/n for n in
        ('CompilerMetadata.cs','NativeApi.cs','NativeTrainStreamContext.cs','NativePaidBirthCommands.cs',
         'NativeBuildRecordJournalDraft.cs','NativeBuildAttachmentPortDraft.cs','NativeExchangeCommands.cs',
         'NativeTrainLeafProbeAdapter.cs','NativeTrainLeafDispatcher.cs','NativeTrainFactoryTraversal.cs',
         'NativeBuildLeafProbeAdapter.cs','NativeBuildLeafDispatcher.cs','NativeBuildFactoryTraversalDraft.cs',
         'NativePrivateFactoryObserverRouteDraft.cs','NativeBuildFactoryHelperDriverDraft.cs')]+[
        work/'BuildJournalSmuggleStore.cs',work/'BuildJournalContextFixture.cs',
        ROOT/'tests/train-leaf/NativeTrainLeafProbeDraftTests.cs',
        ROOT/'tests/build-journal/NativeBuildJournalDraftTests.cs',
        ROOT/'tests/build-factory/NativeBuildFactoryDraftTests.cs']

def build_journal_sources(work):
    # Exercise the actual stores without the unrelated live gameplay hooks.
    # These are exact source slices, not a second binding implementation.
    s=regular(ROOT/'src/PolyMod/MimicSmuggleActions.cs').read_text()
    def part(start,end):
        if s.count(start)!=1 or s.count(end)!=1:raise Stop('Smuggle fixture extraction seam changed.')
        return s[s.index(start):s.index(end)]
    pieces=[part('        internal const uint PayloadMagic','        const string IT='),
            part('        sealed class Payload\n','        internal sealed class CommandScope\n'),
            part('        public static void ForgetOffer','        public static void Install('),
            part('        static string? IdOf','        static object? Player')]
    store=work/'BuildJournalSmuggleStore.cs'
    store.write_text('using System;using System.IO;using System.Linq;using System.Collections.Generic;\nnamespace Deception.PolyModAdapter { internal static class MimicSmuggleActions {\n'+''.join(pieces)+'\n}}\n')
    original=regular(ROOT/'tests/train-leaf/NativeTrainFactoryAdmissionDraftTests.cs').read_text()
    old='{1000=>NativePaidBirthIntentCodec.Warrior,1001=>NativePaidBirthIntentCodec.Rider,_=>"warrior"};'
    if original.count(old)!=1:raise Stop('Build fixture EnumCache seam changed.')
    context=work/'BuildJournalContextFixture.cs'
    context.write_text(original.replace(old,'{2000=>"dcp_smuggle_fruit",2001=>NativeExchangeCatalog.Id(NativeExchangeKind.Regroup),2002=>NativeExchangeCatalog.Id(NativeExchangeKind.Disguise),2003=>"dcp_unknown",1000=>NativePaidBirthIntentCodec.Warrior,1001=>NativePaidBirthIntentCodec.Rider,_=>"warrior"};'))
    return sorted((ROOT/'src/Core').glob('*.cs'))+[ROOT/'src/PolyMod'/n for n in
        ('CompilerMetadata.cs','NativeApi.cs','NativeTrainStreamContext.cs','NativePaidBirthCommands.cs',
         'NativeBuildRecordJournalDraft.cs','NativeBuildAttachmentPortDraft.cs','NativeExchangeCommands.cs',
         'NativeBuildLeafProbeAdapter.cs','NativeBuildLeafDispatcher.cs')]+[store,context]+sorted((ROOT/'tests/build-journal').glob('*.cs'))

# Reviewable fragment for the lead to integrate into project/tools/manage.py.
# This file is not a standalone runner and makes no calls when imported.
REGROUP_ADAPTER_SOURCES = (
    'NativeExchangeCommands.cs', 'NativeRegroupActions.cs', 'MimicRegroupUi.cs',
)

def regroup_fixture_sources():
    return (sorted((ROOT / 'src/Core').glob('*.cs')) +
            [ROOT / 'src/PolyMod' / name for name in UI_ADAPTER_SOURCES] +
            [ROOT / 'src/PolyMod' / name for name in REGROUP_ADAPTER_SOURCES] +
            sorted((ROOT / 'tests/regroup').glob('*.cs')) + [ROOT/'tests/ui-adapter/MapPrefabFixture.cs',ROOT/'tests/ui-adapter/UpgradeUiInactiveFixture.cs',ROOT/'tests/ui-adapter/KnowledgeInactiveFixture.cs',ROOT/'tests/ui-adapter/AttachmentWeakIdentityFixture.cs'])

def validate_regroup_results(results):
    expected = json.loads(regular(ROOT / 'tests/regroup/expected-regroup-tests.json').read_text())['ids']
    tests = results.get('tests', [])
    found = [case.get('id') for case in tests]
    if (len(expected) != len(set(expected)) or len(found) != len(set(found)) or
            set(found) != set(expected) or results.get('total') != len(expected) or
            results.get('passed') != len(expected) or results.get('failed') != 0 or
            any(case.get('status') != 'PASS' for case in tests)):
        raise Stop('Regroup callback/scaffold fixture failed or changed coverage.')

def run_regroup_fixture(dotnet, compiler, work, refs, log, host, runtime, portrait_resource):
    csc(dotnet, compiler, regroup_fixture_sources(), work / 'Deception.RegroupFixture.dll',
        refs, log, resources=portrait_resource)
    env = clean_env()
    env['DECEPTION_REGROUP_FIXTURE_OUTPUT'] = str(work / 'regroup-fixture-results.json')
    call(['/usr/bin/arch', '-x86_64', host, runtime, work,
          'Deception.RegroupFixture', 'Deception.Tests.RegroupFixtureTests', 'Run'],
         log=log, env=env)
    validate_regroup_results(json.loads(regular(work / 'regroup-fixture-results.json').read_text()))


def validate_regroup_planning(result):
    expected=json.loads(regular(ROOT/'tests/regroup-planning/expected.json').read_text())
    if result.get('tests')!=len(expected['names']) or result.get('assertions')!=expected['assertions'] or result.get('failed')!=0 or result.get('names')!=expected['names']:
        raise Stop('Regroup planning readiness/terrain/identity fixture failed or changed coverage.')

def run_regroup_planning(dotnet,compiler,work,refs,log,host,runtime):
    sources=sorted((ROOT/'src/Core').glob('*.cs'))+[ROOT/'src/PolyMod'/n for n in
        ('NativeExchangePlan.cs','NativeExchangePlanning.cs','NativeLedgerState.cs','NativeDisguiseConcealmentEffects.cs','NativeAquarionUnits.cs','CompilerMetadata.cs')]+sorted((ROOT/'tests/regroup-planning').glob('*.cs'))
    csc(dotnet,compiler,sources,work/'Deception.PlanningFixtureTests.dll',refs,log)
    env=clean_env();env['PLANNING_TEST_OUTPUT']=str(work/'regroup-planning-results.json')
    call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.PlanningFixtureTests','Deception.Tests.PlanningFixtureTests','Run'],log=log,env=env)
    validate_regroup_planning(json.loads(regular(work/'regroup-planning-results.json').read_text()))

def validate_registry_order(result):
    expected=json.loads(regular(ROOT/'tests/registry-order/expected.json').read_text())
    if result.get('tests')!=len(expected['names']) or result.get('assertions')!=expected['assertions'] or result.get('failed')!=0 or result.get('names')!=expected['names']:
        raise Stop('Native registry order fixture failed or changed coverage.')

def run_registry_order(dotnet,compiler,work,refs,log,host,runtime):
    sources=sorted((ROOT/'src/Core').glob('*.cs'))+[ROOT/'src/PolyMod'/n for n in
        ('NativeRegistryOrder.cs','NativeRegistryPublication.cs','CompilerMetadata.cs')]+sorted((ROOT/'tests/registry-order').glob('*.cs'))
    csc(dotnet,compiler,sources,work/'Deception.RegistryOrderFixture.dll',refs,log)
    env=clean_env();env['REGISTRY_TEST_OUTPUT']=str(work/'registry-order-results.json');env['CAPTURED_TREE']=str(ROOT/'tests/native-tree.captured.json')
    call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.RegistryOrderFixture','Deception.Tests.RegistryFixtureTests','Run'],log=log,env=env)
    validate_registry_order(json.loads(regular(work/'registry-order-results.json').read_text()))

def validate_regroup_animation(results):
    if results.get('cases')!=49 or results.get('assertions')!=105 or results.get('passed') is not True:
        raise Stop('Regroup animation fixture failed or changed coverage.')

def validate_badge_alpha(results):
    if results.get('assertions')!=4227 or results.get('passed') is not True:
        raise Stop('Native badge alpha regression failed or changed coverage.')

def validate_map_frame(results):
    if results.get('cases')!=6 or results.get('assertions')!=62 or results.get('passed') is not True:
        raise Stop('Warrior map frame regression failed or changed coverage.')

def validate_disguise_refresh(results):
    if (results.get('passed') is not True or results.get('cases')!=38 or results.get('assertions')!=58 or
            results.get('deniedContexts')!=28 or results.get('idleRefreshes')!=50):
        raise Stop('Current disguise visual lifecycle regression failed or changed coverage.')

def run_disguise_refresh(dotnet,compiler,work,refs,log,host,runtime):
    sources=[ROOT/'src/PolyMod'/name for name in ('NativeApi.cs','NativeDisguiseRefresh.cs')]+sorted((ROOT/'tests/disguise-refresh-0354').glob('*.cs'))
    csc(dotnet,compiler,sources,work/'Deception.DisguiseRefreshTests.dll',refs,log)
    output=call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.DisguiseRefreshTests','Deception.Tests.RefreshTests','Run'],log=log,env=clean_env())
    result=json.loads(next(line for line in output.splitlines() if line.startswith('{')))
    validate_disguise_refresh(result)
    (work/'disguise-refresh-results.json').write_text(json.dumps(result,indent=2)+'\n')

def validate_disguise_growth(result):
    if result.get('passed') is not True or result.get('cases')!=40 or result.get('assertions')!=147 or result.get('idleBoundaries')!=50:
        raise Stop('Private owner-turn growth fixture failed or changed coverage.')

def validate_growth_audio(result):
    expected=json.loads(regular(ROOT/'tests/growth-audio-0358/expected.json').read_text())
    if result.get('passed') is not True or result.get('cases')!=expected['cases'] or result.get('assertions')!=expected['assertions']:
        raise Stop('Native disguise growth audio fixture failed or changed coverage.')

def validate_mimic_prefab(result):
    if result.get('status')!='PASS' or result.get('cases')!=20 or result.get('assertions')!=869:
        raise Stop('Exact native disguise prefab fixture failed or changed coverage.')

def validate_growth_sight(result):
    if result.get('passed') is not True or result.get('cases')!=68 or result.get('assertions')!=260:
        raise Stop('Native growing sight staging fixture failed or changed coverage.')

def run_growing_disguises(dotnet,compiler,work,refs,log,host,runtime):
    for key,folder,adapter,assembly,entry,validator,core in (
        ('disguise-growth','disguise-growth-0355','NativeDisguiseGrowth.cs','Deception.GrowthTests','Deception.Tests.GrowthTests',validate_disguise_growth,True),
        ('growth-audio','growth-audio-0358','NativeDisguiseGrowthAudio.cs','Deception.GrowthAudioTests','Deception.Tests.GrowthAudioTests',validate_growth_audio,True),
        ('mimic-prefab','prefab-0355','NativeMimicPrefabLookup.cs','Deception.PrefabTests','Deception.Tests.PrefabTests',validate_mimic_prefab,False),
        ('growth-sight','growth-sight-0355','NativeGrowthSight.cs','Deception.GrowthSightTests','Deception.Tests.SightTests',validate_growth_sight,False)):
        sources=([*sorted((ROOT/'src/Core').glob('*.cs'))] if core else [])+[ROOT/'src/PolyMod'/name for name in ('NativeApi.cs',adapter)]+sorted((ROOT/'tests'/folder).glob('*.cs'))
        csc(dotnet,compiler,sources,work/(assembly+'.dll'),refs,log)
        output=call(['/usr/bin/arch','-x86_64',host,runtime,work,assembly,entry,'Run'],log=log,env=clean_env())
        result=json.loads(next(line for line in output.splitlines() if line.startswith('{')))
        validator(result)
        (work/(key+'-results.json')).write_text(json.dumps(result,indent=2)+'\n')

def validate_mimic_transport(result):
    expected=json.loads(regular(ROOT/'tests/mimic-transport-0357/expected.json').read_text())
    if result.get('passed') is not True or result.get('cases')!=expected['cases'] or result.get('assertions')!=expected['assertions']:
        raise Stop('Naval transport and coastal exchange fixture failed or changed coverage.')

def validate_disguise_lifecycle(result):
    if result.get('passed') is not True or result.get('cases')!=33 or result.get('assertions')!=71:
        raise Stop('Disguise event lifecycle checks failed or changed coverage.')

def validate_naval_presentation(result):
    if result.get('passed') is not True or result.get('cases')!=16 or result.get('assertions')!=108:
        raise Stop('Naval and genuine disguise presentation fixture failed or changed coverage.')

def run_mimic_transport(dotnet,compiler,work,refs,log,host,runtime):
    sources=sorted((ROOT/'src/Core').glob('*.cs'))+[ROOT/'src/PolyMod'/name for name in ('NativeApi.cs','NativeMimicTransport.cs','NativeExchangePlan.cs','NativeExchangePlanning.cs','NativeExchangeCommit.cs','NativeShedDisguise.cs','NativeDisguiseConcealmentEffects.cs')]+sorted((ROOT/'tests/mimic-transport-0357').glob('*.cs'))
    csc(dotnet,compiler,sources,work/'Deception.TransportTests.dll',refs,log)
    env=clean_env();env['TRANSPORT_TEST_OUTPUT']=str(work/'mimic-transport-results.json')
    call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.TransportTests','Deception.Tests.TransportTests','Run'],log=log,env=env)
    result=json.loads(regular(work/'mimic-transport-results.json').read_text())
    validate_mimic_transport(result)

def run_disguise_lifecycle(dotnet,compiler,work,refs,log,host,runtime):
    sources=sorted((ROOT/'src/Core').glob('*.cs'))+[ROOT/'src/PolyMod'/name for name in ('NativeApi.cs','NativeDisguiseLifecycle.cs','NativeDisguiseConcealmentEffects.cs')]+sorted((ROOT/'tests/disguise-lifecycle-0357').glob('*.cs'))
    csc(dotnet,compiler,sources,work/'Deception.LifecycleTests.dll',refs,log)
    output=call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.LifecycleTests','Deception.Tests.LifecycleTests','Run'],log=log,env=clean_env())
    result=json.loads(next(line for line in output.splitlines() if line.startswith('{')))
    validate_disguise_lifecycle(result)
    (work/'disguise-lifecycle-results.json').write_text(json.dumps(result,indent=2)+'\n')

def validate_candidate_0359(result,folder):
    expected=json.loads(regular(ROOT/'tests'/folder/'expected.json').read_text())
    if 'names' in expected and 'cases' not in expected:
        if result.get('failed')!=0 or result.get('tests')!=len(expected['names']) or result.get('assertions')!=expected['assertions'] or result.get('names')!=expected['names']:
            raise Stop('Native candidate fixture failed or changed coverage: '+folder)
        return
    success=(expected['status']=='PASS' and result.get('status')=='PASS') if 'status' in expected else result.get('passed') is True
    if not success or result.get('cases')!=expected['cases'] or result.get('assertions')!=expected['assertions']:
        raise Stop('Native candidate fixture failed or changed coverage: '+folder)
    for field in ('results','tests'):
        if field in expected and result.get(field)!=expected[field]:
            raise Stop('Native candidate fixture case identities changed: '+folder)

def run_candidate_0359(dotnet,compiler,work,refs,log,host,runtime):
    for key,folder,adapters,assembly,entry in (
        ('lighthouse-secret','lighthouse-secret-0398',('NativeLighthouseSecret.cs','NativeLighthouseSecretPresentation.cs','NativeExchangePlan.cs'),'Deception.LighthouseSecretTests','Deception.Tests.LighthouseSecretTests'),
        ('technology-intelligence','technology-intelligence-0390',('NativeTradeSecrets.cs','NativeTechnologyIntelligence.cs'),'Deception.IntelTests','Deception.Tests.IntelTests'),
        ('disband-presentation','disband-presentation-0380',('NativeDisbandPresentation.cs',),'Deception.DisbandPresentationTests','Deception.Tests.DisbandPresentationTests'),
        ('completed-sight','rogue-sight-0378',('NativeGameStateSnapshot.cs','NativeLedgerState.cs','NativeDisguiseConcealmentEffects.cs','NativeRogueSight.cs'),'Deception.CompletedSightTests','Deception.Tests.CompletedSightTests'),
        ('rogue-combat','rogue-combat-0378',('NativeSpyNetwork.cs','NativeRogueRules.cs','NativeRogueTactics.cs','NativeRogueAccounting.cs','NativeFeignSupportEffects.cs','NativeInvestment.cs'),'Deception.RogueCombatTests','Deception.Tests.RogueCombatTests'),
        ('ordinary-actions','ordinary-actions-0377',('RecruitmentHooks.cs','OrdinaryActionEligibility.cs','NativeOrdinaryCommands.cs','NativeSpyActions.cs','NativeInvestment.cs','NativeRogueRules.cs'),'Deception.OrdinaryActionTests','Deception.Tests.OrdinaryActionTests'),
        ('shed-presentation','shed-presentation-0373',('NativeShedPresentation.cs',),'Deception.ShedPresentationTests','ShedPresentationTests'),
        ('flood-transport','flood-transport-0371',('NativeMimicTransport.cs','NativeExchangePlan.cs','NativeExchangePlanning.cs','NativeExchangeCommit.cs','NativeShedDisguise.cs','NativeDisguiseConcealmentEffects.cs'),'Deception.FloodTransportTests','Deception.Tests.FloodTransportTests'),
        ('path-preference','path-preference-0375',('NativeSpyMovementProfile.cs','NativeDisguiseConcealment.cs','NativeMimicTransport.cs','NativeExchangePlan.cs','NativeExchangePlanning.cs','NativeExchangeCommit.cs','NativeApparentUpgrades.cs','NativeShedDisguise.cs','NativeDisguiseConcealmentEffects.cs','NativeMimicWaterMovement.cs','NativeDisguiseMovement.cs','NativeMimicMovement.cs','NativeCloakMovement.cs','NativeRogueTactics.cs','NativeDisguisePathPreference.cs'),'Deception.PathPreferenceTests','Deception.Tests.PathPreferenceTests'),
        ('mimic-movement','mimic-movement-0370',('NativeSpyMovementProfile.cs','NativeMimicTransport.cs','NativeExchangePlan.cs','NativeExchangePlanning.cs','NativeExchangeCommit.cs','NativeApparentUpgrades.cs','NativeShedDisguise.cs','NativeDisguiseConcealmentEffects.cs','NativeMimicWaterMovement.cs','NativeDisguiseMovement.cs','NativeMimicMovement.cs','NativeCloakMovement.cs','NativeRogueTactics.cs'),'Deception.MimicMovementTests','Deception.Tests.MimicMovementTests'),
        ('rogue-visuals','rogue-visuals-0370',('NativeSpyActions.cs','NativeSpyNetwork.cs','NativeSpyReadinessPresentation.cs','NativeRogueVisuals.cs'),'Deception.RogueVisualTests','Deception.Tests.RogueVisualTests'),
        ('spy-effects','spy-effects-0370',('ModContentAssets.cs','SmokeGrenadeAssets.cs','NativeSpyEffectPresentation.cs'),'Deception.SpyEffectTests','SpyEffectTests'),
        ('blackmail-hostility','blackmail-hostility-0369',('NativeBlackmailHostility.cs',),'Deception.HostilityTests','Deception.Tests.HostilityTests'),
        ('blackmail-notices','blackmail-notices-0369',('NativeBlackmail.cs','NativeExchangePlan.cs','NativeBlackmailNotices.cs'),'Deception.NoticeTests','Deception.Tests.NoticeTests'),
        ('blackmail-income','blackmail-income-0369',('NativeBlackmail.cs','NativeExchangePlan.cs','NativeBlackmailIncomeUi.cs'),'Deception.IncomeTests','Deception.Tests.IncomeTests'),
        ('blackmail','blackmail-0368',('NativeBlackmail.cs','NativeBlackmailContacts.cs','NativeBlackmailHostility.cs','NativeExchangePlan.cs'),'Deception.BlackmailTests','Deception.Tests.BlackmailTests'),
        ('shed-water','shed-water-0368',('NativeMimicTransport.cs','NativeExchangePlan.cs','NativeExchangePlanning.cs','NativeExchangeCommit.cs','NativeApparentUpgrades.cs','NativeShedDisguise.cs','NativeDisguiseConcealmentEffects.cs','NativeMimicWaterMovement.cs','MimicPresentationState.cs'),'Deception.ShedWaterTests','Deception.Tests.ShedWaterTests'),
        ('disguise-movement','disguise-movement-0367',('NativeSpyMovementProfile.cs','NativeDisguiseLifecycle.cs','NativeDisguiseConcealmentEffects.cs','NativeMimicWaterMovement.cs','NativeDisguiseMovement.cs','NativeApparentTerrain.cs'),'Deception.DisguiseMovementTests','Deception.Tests.MovementTests'),
        ('exchange-transformation','exchange-transformation-0367',('NativeGameStateSnapshot.cs','NativeExchangeTransformationVisual.cs'),'Deception.TransformationTests','Deception.Tests.TransformationTests'),
        ('combat-preview','combat-preview-0367',('NativeGameStateSnapshot.cs','NativeCombatPreview.cs'),'Deception.CombatPreviewTests','Deception.Tests.CombatPreviewTests'),
        ('spy-readiness','spy-readiness-0367',('NativeSpyActions.cs','NativeSpyNetwork.cs','NativeSpyReadinessPresentation.cs'),'Deception.SpyReadinessTests','Deception.Tests.SpyReadinessTests'),
        ('mimic-water','mimic-water-0366',('NativeSpyMovementProfile.cs','NativeMimicWaterMovement.cs','NativeCloakMovement.cs'),'Deception.WaterMovementTests','Deception.Tests.WaterMovementTests'),
        ('apparent-splash','apparent-splash-0366',('NativeApparentSplashPresentation.cs',),'Deception.SplashTests','Deception.Tests.SplashTests'),
        ('hotseat-session','session-0363',('NativeSavedMatchSummary.cs','NativeMenuPreviewScope.cs','NativeRegroupSession.cs','NativeRegroupPaidBirth.cs','NativeAquarionUnits.cs','NativeGameStateSnapshot.cs'),'Deception.SessionTests','Deception.Tests.SessionTests'),
        ('rogue-upgrade','rogue-upgrade-0362',('NativeRogueUpgrade.cs',),'Deception.RogueUpgradeTests','Deception.Tests.RogueUpgradeTests'),
        ('rogue-sight','rogue-sight-0362',('NativeRogueSight.cs',),'Deception.RogueSightTests','Deception.Tests.RogueSightTests'),
        ('rogue','rogue-0362',('NativeSpyNetwork.cs','NativeRogueRules.cs','NativeRogueTactics.cs','NativeRogueAccounting.cs','NativeFeignSupportEffects.cs','NativeInvestment.cs'),'Deception.RogueTests','Deception.Tests.RogueTests'),
        ('feign-support','feign-support-0362',('NativeFeignSupport.cs','NativeFeignSupportEffects.cs','NativeFeignSupportReaction.cs','NativeDisguiseLifecycle.cs','NativeDisguiseConcealmentEffects.cs'),'Deception.FeignSupportTests','Deception.Tests.SupportTests'),
        ('elyrion-eggs','elyrion-eggs-0396',('NativeExchangeDiagnosticResearch.cs',),'Deception.ElyrionEggTests','Deception.Tests.ElyrionEggTests'),
        ('blackout','blackout-0362',('NativeDisguiseConcealmentEffects.cs','NativeSpyNetwork.cs','NativeBlackout.cs','NativeBlackoutDiscovery.cs','NativeBlackoutLighthouse.cs','NativeBlackoutAccess.cs','NativeSubvert.cs','NativeBlackoutUi.cs','NativeExchangePlan.cs','NativeRogueSight.cs'),'Deception.BlackoutTests','Deception.Tests.BlackoutTests'),
        ('blackout-connections','blackout-connections-0393',('NativeDisguiseConcealmentEffects.cs','NativeSpyNetwork.cs','NativeBlackout.cs','NativeBlackoutDiscovery.cs','NativeBlackoutLighthouse.cs','NativeBlackoutAccess.cs','NativeSubvert.cs','NativeBlackoutUi.cs','NativeExchangePlan.cs','NativeRogueSight.cs'),'Deception.BlackoutConnectionTests','Deception.Tests.BlackoutTests'),
        ('fogger-actions','fogger-actions-0390',('NativeDisguiseConcealmentEffects.cs','NativeSpyNetwork.cs','NativeBlackout.cs','NativeBlackoutDiscovery.cs','NativeBlackoutLighthouse.cs','NativeBlackoutAccess.cs','NativeSubvert.cs','NativeBlackoutUi.cs','NativeExchangePlan.cs','NativeRogueSight.cs'),'Deception.FoggerActionTests','Deception.Tests.FoggerActionTests'),
        ('lighthouse','blackout-lighthouse-0373',('NativeDisguiseConcealmentEffects.cs','NativeSpyNetwork.cs','NativeBlackout.cs','NativeBlackoutDiscovery.cs','NativeBlackoutLighthouse.cs','NativeBlackoutAccess.cs','NativeSubvert.cs','NativeBlackoutUi.cs','NativeExchangePlan.cs','NativeRogueSight.cs'),'Deception.LighthouseTests','Deception.Tests.LighthouseTests'),
        ('apparent-terrain','apparent-terrain-0362',('NativeApparentTerrain.cs',),'Deception.TerrainTests','Deception.Tests.TerrainTests'),
        ('shed-disguise','shed-disguise-0361',('NativeMimicTransport.cs','NativeExchangePlan.cs','NativeExchangePlanning.cs','NativeExchangeCommit.cs','NativeApparentUpgrades.cs','NativeShedDisguise.cs','NativeDisguiseConcealmentEffects.cs'),'Deception.ShedTests','Deception.Tests.ShedTests'),
        ('coastal-recruitment','coastal-recruitment-0361',('RecruitmentHooks.cs','NativeAquarionUnits.cs'),'Deception.CoastalRecruitmentTests','Deception.Tests.CoastalRecruitmentTests'),
        ('cloak-readiness','cloak-readiness-0361',('NativeDisguiseConcealment.cs','NativeDisguiseConcealmentEffects.cs','NativeDisguiseLifecycle.cs','NativeDisguiseConcealmentVisual.cs'),'Deception.CloakReadinessTests','Deception.Tests.CloakReadinessTests'),
        ('knowledge','intelligence-0361',('NativeIntelligenceOverlay.cs','NativeSurveillanceUi.cs','MimicHoldInput.cs'),'Deception.KnowledgeTests','Deception.Tests.KnowledgeTests'),
        ('menu-preview','menu-preview-0361',('NativeSavedMatchSummary.cs','NativeMenuPreviewScope.cs','NativeRegroupSession.cs','NativeGameStateSnapshot.cs'),'Deception.MenuPreviewTests','Deception.Tests.MenuPreviewTests'),
        ('hook-abi','hook-abi-0360',(),'Deception.HookAbiTests','Deception.Tests.HookAbiTests'),
        ('cloak-movement','cloak-movement-0359',('NativeSpyMovementProfile.cs','NativeCloakMovement.cs',),'Deception.CloakMovementTests','Deception.Tests.CloakMovementTests'),
        ('concealment','concealment-0359',('NativeDisguiseConcealment.cs','NativeDisguiseConcealmentEffects.cs'),'Deception.ConcealmentTests','Deception.Tests.ConcealmentTests'),
        ('apparent-upgrades','apparent-upgrades-0359',('NativeMimicTransport.cs','NativeExchangePlan.cs','NativeExchangePlanning.cs','NativeExchangeCommit.cs','NativeApparentUpgrades.cs','NativeShedDisguise.cs','NativeApparentUpgradeUi.cs','NativeDisguiseConcealmentEffects.cs'),'Deception.UpgradeTests','Deception.Tests.UpgradeTests')):
        core=[] if key in ('hook-abi','spy-effects') else sorted((ROOT/'src/Core').glob('*.cs'))
        sources=core+[ROOT/'src/PolyMod'/name for name in ((*adapters,) if key in ('shed-presentation','elyrion-eggs') else ('NativeApi.cs',*adapters))]+sorted((ROOT/'tests'/folder).glob('*.cs'))
        if 'NativeSpyActions.cs' in adapters or 'NativeSpyNetwork.cs' in adapters:sources.append(ROOT/'src/PolyMod/NativeFoggerActions.cs')
        if key in ('hotseat-session','menu-preview'):sources.append(ROOT/'tests/ports/OnlineDisabledPort.cs')
        if key in ('lighthouse','fogger-actions','blackout-connections'):sources += sorted((ROOT/'tests/blackout-0362').glob('*.cs'))
        if key=='flood-transport':sources += [ROOT/'tests/mimic-transport-0357'/name for name in ('FixturePorts.cs','NewCommandPorts.cs')]
        if key=='cloak-readiness':sources.append(ROOT/'tests/concealment-0359/Fixture.cs')
        effect_resources=[(p,'Deception.SpyEffects.'+p.name) for p in sorted((ROOT/'content/spy-effects').glob('*')) if p.suffix in ('.png','.wav')] if key=='spy-effects' else []
        csc(dotnet,compiler,sources,work/(assembly+'.dll'),refs,log,resources=effect_resources)
        output=call(['/usr/bin/arch','-x86_64',host,runtime,work,assembly,entry,'RunConnections' if key=='blackout-connections' else 'Run'],log=log,env={**clean_env(),'DECEPTION_CAPTURED_TREE_FIXTURE':str(ROOT/'tests/native-tree.captured.json')})
        result=json.loads(next(line for line in output.splitlines() if line.startswith('{')))
        validate_candidate_0359(result,folder)
        (work/(key+'-results.json')).write_text(json.dumps(result,indent=2)+'\n')

def run_online_fixtures(dotnet,compiler,work,refs,log,host,runtime):
    core=sorted((ROOT/'src/Core').glob('*.cs'))
    def adapters(*names):return [ROOT/'src/PolyMod'/name for name in names]
    recipes=(
        ('online-protocol','online-sync','Deception.OnlineProtocolTests','Deception.Tests.OnlineProtocolTests',
         core+adapters('NativeExchangePlan.cs','NativeExchangePlanning.cs','NativeLedgerState.cs','NativeDisguiseConcealmentEffects.cs','NativeAquarionUnits.cs','CompilerMetadata.cs')+
         [ROOT/'tests/regroup-planning'/n for n in ('PlanningFixturePorts.cs','NewCommandPorts.cs')]),
        ('online-session','online-session-0383','Deception.OnlineSessionTests','Deception.Tests.OnlineSessionTests',
         core+adapters('NativeApi.cs','NativeSavedMatchSummary.cs','NativeMenuPreviewScope.cs','NativeRegroupSession.cs','NativeRegroupPaidBirth.cs','NativeAquarionUnits.cs','NativeGameStateSnapshot.cs','NativeOnlineSession.cs')),
        ('online-preview','online-preview','Deception.OnlinePreviewTests','Deception.Tests.OnlinePreviewTests',
         [ROOT/'src/Core/NativeStateFrame.cs']+adapters('NativeOnlinePreview.cs','CompilerMetadata.cs')),
        ('online-dispatch','online-dispatch','Deception.OnlineDispatchTests','Deception.Tests.OnlineDispatchTests',
         adapters('NativeOnlineDispatch.cs','CompilerMetadata.cs')),
        ('online-resign','online-resign','Deception.OnlineResignTests','Deception.Tests.OnlineResignTests',
         adapters('NativeOnlineResign.cs','NativeOnlineDispatch.cs','CompilerMetadata.cs')),
        ('online-maintenance','online-maintenance','Deception.OnlineMaintenanceTests','Deception.Tests.OnlineMaintenanceTests',
         adapters('NativeOnlineMaintenance.cs','CompilerMetadata.cs')),
    )
    for key,folder,assembly,entry,sources in recipes:
        sources=sources+sorted((ROOT/'tests'/folder).glob('*.cs'))
        csc(dotnet,compiler,sources,work/(assembly+'.dll'),refs,log)
        output=call(['/usr/bin/arch','-x86_64',host,runtime,work,assembly,entry,'Run'],log=log,env=clean_env())
        result=json.loads(next(line for line in output.splitlines() if line.startswith('{')))
        validate_candidate_0359(result,folder)
        (work/(key+'-results.json')).write_text(json.dumps(result,indent=2)+'\n')

def run_regroup_animation(dotnet,compiler,work,refs,log,host,runtime):
    sources=[ROOT/'src/PolyMod'/name for name in ('NativeApi.cs','MimicRegroupAnimation.cs')]+[ROOT/'tests/regroup-animation/AnimationFixture.cs']
    csc(dotnet,compiler,sources,work/'Deception.AnimationTests.dll',refs,log)
    output=call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.AnimationTests','AnimationTests','Run'],log=log,env=clean_env())
    result=json.loads(next(line for line in output.splitlines() if line.startswith('{')))
    validate_regroup_animation(result)
    (work/'regroup-animation-results.json').write_text(json.dumps(result,indent=2)+'\n')

def validate_regroup_ledger(result):
    expected=json.loads(regular(ROOT/'tests/regroup-ledger/expected.json').read_text())
    if result.get('tests')!=len(expected) or result.get('assertions')!=1480 or result.get('failed')!=0 or result.get('names')!=expected:
        raise Stop('Regroup ledger lifecycle checks failed or changed coverage.')

def run_regroup_ledger(dotnet,compiler,work,refs,log,host,runtime):
    sources=sorted((ROOT/'src/Core').glob('*.cs'))+[ROOT/'src/PolyMod'/name for name in ('NativeLedgerState.cs','NativeDisguiseConcealmentEffects.cs','NativeAquarionUnits.cs','CompilerMetadata.cs')]+[ROOT/'tests/regroup-ledger/LedgerLifecycleTests.cs']
    csc(dotnet,compiler,sources,work/'Deception.LedgerLifecycleTests.dll',refs,log)
    env=clean_env();env['LEDGER_TEST_OUTPUT']=str(work/'regroup-ledger-results.json')
    call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.LedgerLifecycleTests','Deception.Tests.LedgerLifecycleTests','Run'],log=log,env=env)
    validate_regroup_ledger(json.loads(regular(work/'regroup-ledger-results.json').read_text()))

def validate_ledger_read(result):
    if result.get('passed') is not True or result.get('assertions')!=11 or result.get('reads')!=10000 or result.get('largeUnits')!=4097:
        raise Stop('Ledger presentation read checks failed or changed coverage.')
    small=result.get('smallReadBytes',-1);large=result.get('largeReadBytes',-1)
    if not 0<=small<2000000 or not 0<=large<2000000 or abs(small-large)>1024:
        raise Stop('Ledger presentation reads allocate with ledger size.')

def run_ledger_read(dotnet,compiler,work,refs,log,host,runtime):
    sources=sorted((ROOT/'src/Core').glob('*.cs'))+[ROOT/'src/PolyMod/NativeGameStateSnapshot.cs',ROOT/'src/PolyMod/CompilerMetadata.cs',ROOT/'tests/ledger-read/SnapshotTests.cs']
    csc(dotnet,compiler,sources,work/'Deception.SnapshotTests.dll',refs,log)
    output=call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.SnapshotTests','Deception.Tests.SnapshotTests','Run'],log=log,env=clean_env())
    result=json.loads(next(line for line in output.splitlines() if line.startswith('{')))
    validate_ledger_read(result)
    (work/'ledger-read-results.json').write_text(json.dumps(result,indent=2)+'\n')

def build(game):
    before=preflight(game);source=same_tree_sources()
    build_root=ROOT/'build';build_root.mkdir(exist_ok=True)
    work=Path(tempfile.mkdtemp(prefix='m0-',dir=str(build_root)))
    print('Build/test directory: '+str(work),flush=True)
    print('Offline source build: %d model tests + %d production UI-adapter checks with synthetic native objects, under bundled Intel .NET. No game launch.'%(len(expected_model_ids()),len(expected_ui_adapter_ids())),flush=True)
    with (work/'build.log').open('w') as log:
        audit_args=[sys.executable,ROOT/'tools/audit_native_api.py',game/'BepInEx/interop',work/'native-api-results.json']
        from public_polymod import SHA256 as PUBLIC_POLYMOD_SHA
        if CONTRACT['framework_checksums']['BepInEx/plugins/PolyMod.dll']==PUBLIC_POLYMOD_SHA:audit_args.append('--public-polymod')
        from clean_beta import SHA256 as CLEAN_BETA_SHA
        if CONTRACT['framework_checksums']['BepInEx/plugins/PolyMod.dll']==CLEAN_BETA_SHA:audit_args.append('--clean-beta')
        call(audit_args,log=log)
        dotnet,compiler=sdk();runtime=game/'dotnet'
        refs=sorted(p for p in runtime.glob('*.dll') if p.is_file())
        for name in ('System.Private.CoreLib.dll','System.Runtime.dll','System.Text.Json.dll'):
            regular(runtime/name)
        core=sorted((ROOT/'src/Core').glob('*.cs'))
        tests=sorted((ROOT/'tests').glob('*.cs'))
        csc(dotnet,compiler,core+tests,work/'Deception.ModelTests.dll',refs,log)
        host=work/'model-test-host'
        call(['/usr/bin/clang','-arch','x86_64','-O2','-Wall','-Wextra','-Werror',ROOT/'tools/coreclr-probe.c','-o',host],log=log)
        call(['/usr/bin/lipo',host,'-verify_arch','x86_64'],log=log)
        env=clean_env();env['DECEPTION_TEST_OUTPUT']=str(work/'model-test-results.json');env['DECEPTION_TREE_FIXTURE']=str(ROOT/'tests/native-tree.synthetic.json');env['DECEPTION_CAPTURED_TREE_FIXTURE']=str(ROOT/'tests/native-tree.captured.json')
        call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.ModelTests','Deception.Tests.KernelTests','Run'],log=log,env=env)
        results=json.loads(regular(work/'model-test-results.json').read_text())
        validate_model_results(results)
        # These compile the ACTUAL UI adapter files against an explicit stand-in
        # surface. They test managed control flow, not Unity/Harmony/native behavior.
        ui_sources=core+[ROOT/'src/PolyMod'/n for n in UI_ADAPTER_SOURCES]+[ROOT/'src/PolyMod/NativeBlackmailPresentation.cs',ROOT/'src/PolyMod/NativeBlackmailEmbassyUi.cs',ROOT/'src/PolyMod/NativeTechArtPrewarm.cs']+sorted((ROOT/'tests/tech-preload-0375').glob('*.cs'))+sorted((ROOT/'tests/ui-adapter').glob('*.cs'))+sorted((ROOT/'tests/map-badge-0353').glob('*.cs'))+sorted((ROOT/'tests/map-frame-0356').glob('*.cs'))+sorted((ROOT/'tests/naval-presentation-0357').glob('*.cs'))+sorted((ROOT/'tests/presentation-0361').glob('*.cs'))+sorted((ROOT/'tests/special-ui-0362').glob('*.cs'))+sorted((ROOT/'tests/cloak-render-0363').glob('*.cs'))+sorted((ROOT/'tests/action-bar-0366').glob('*.cs'))+sorted((ROOT/'tests/blackmail-presentation-0368').glob('*.cs'))+sorted((ROOT/'tests/aquarion-art-0371').glob('*.cs'))+sorted((ROOT/'tests/spy-sizing-0377').glob('*.cs'))
        portrait_resource=[(ROOT/'content/mimic-portrait.png','Deception.MimicPortrait.png')]
        portrait_resource += [(p,'Deception.UnitArt.'+p.name) for p in sorted((ROOT/'content/unit-art').glob('*.png'))]
        portrait_resource += [(p,'Deception.SpyEffects.'+p.name) for p in sorted((ROOT/'content/spy-effects').glob('*')) if p.suffix in ('.png','.wav')]
        csc(dotnet,compiler,ui_sources,work/'Deception.UiAdapterTests.dll',refs,log,resources=portrait_resource)
        env['DECEPTION_ADAPTER_TEST_OUTPUT']=str(work/'ui-adapter-results.json')
        call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.UiAdapterTests','Deception.Tests.UiAdapterTests','Run'],log=log,env=env)
        ui_results=json.loads(regular(work/'ui-adapter-results.json').read_text())
        validate_ui_adapter_results(ui_results)
        fogger_output=call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.UiAdapterTests','Deception.Tests.FoggerArtTests','Run'],log=log,env=clean_env())
        fogger_results=json.loads(next(line for line in fogger_output.splitlines() if line.startswith('{')))
        validate_candidate_0359(fogger_results,'fogger-art-0390')
        (work/'fogger-art-results.json').write_text(json.dumps(fogger_results,indent=2)+'\n')
        sizing_output=call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.UiAdapterTests','Deception.Tests.SpySizingTests','Run'],log=log,env=clean_env())
        sizing_results=json.loads(next(line for line in sizing_output.splitlines() if line.startswith('{')))
        validate_candidate_0359(sizing_results,'spy-sizing-0377')
        (work/'spy-sizing-results.json').write_text(json.dumps(sizing_results,indent=2)+'\n')
        preload_output=call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.UiAdapterTests','Deception.Tests.TechPreloadTests','Run'],log=log,env=clean_env())
        preload_results=json.loads(next(line for line in preload_output.splitlines() if line.startswith('{')))
        validate_candidate_0359(preload_results,'tech-preload-0375')
        (work/'tech-preload-results.json').write_text(json.dumps(preload_results,indent=2)+'\n')
        aquatic_output=call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.UiAdapterTests','Deception.Tests.AquarionArtTests','Run'],log=log,env=clean_env())
        aquatic_results=json.loads(next(line for line in aquatic_output.splitlines() if line.startswith('{')))
        validate_candidate_0359(aquatic_results,'aquarion-art-0371')
        (work/'aquarion-art-results.json').write_text(json.dumps(aquatic_results,indent=2)+'\n')
        presentation_output=call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.UiAdapterTests','Deception.Tests.Presentation0361Tests','Run'],log=log,env=clean_env())
        presentation_results=json.loads(next(line for line in presentation_output.splitlines() if line.startswith('{')))
        validate_candidate_0359(presentation_results,'presentation-0361')
        (work/'presentation-results.json').write_text(json.dumps(presentation_results,indent=2)+'\n')
        special_output=call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.UiAdapterTests','Deception.Tests.SpecialUiTests','Run'],log=log,env=clean_env())
        special_results=json.loads(next(line for line in special_output.splitlines() if line.startswith('{')))
        validate_candidate_0359(special_results,'special-ui-0362')
        (work/'special-ui-results.json').write_text(json.dumps(special_results,indent=2)+'\n')
        cloak_output=call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.UiAdapterTests','Deception.Tests.CloakRenderTests','Run'],log=log,env=clean_env())
        cloak_results=json.loads(next(line for line in cloak_output.splitlines() if line.startswith('{')))
        validate_candidate_0359(cloak_results,'cloak-render-0363')
        (work/'cloak-render-results.json').write_text(json.dumps(cloak_results,indent=2)+'\n')
        action_output=call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.UiAdapterTests','Deception.Tests.ActionBarTests','Run'],log=log,env=clean_env())
        action_results=json.loads(next(line for line in action_output.splitlines() if line.startswith('{')))
        validate_candidate_0359(action_results,'action-bar-0366')
        (work/'action-bar-results.json').write_text(json.dumps(action_results,indent=2)+'\n')
        blackmail_output=call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.UiAdapterTests','Deception.Tests.BlackmailPresentationTests','Run'],log=log,env=clean_env())
        blackmail_results=json.loads(next(line for line in blackmail_output.splitlines() if line.startswith('{')))
        validate_candidate_0359(blackmail_results,'blackmail-presentation-0368')
        (work/'blackmail-presentation-results.json').write_text(json.dumps(blackmail_results,indent=2)+'\n')
        embassy_output=call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.UiAdapterTests','Deception.Tests.EmbassyUiTests','Run'],log=log,env=clean_env())
        embassy_results=json.loads(next(line for line in embassy_output.splitlines() if line.startswith('{')))
        validate_candidate_0359(embassy_results,'blackmail-embassy-ui-0369')
        (work/'blackmail-embassy-ui-results.json').write_text(json.dumps(embassy_results,indent=2)+'\n')
        badge_output=call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.UiAdapterTests','Deception.Tests.BadgeAlphaTests','Run'],log=log,env=clean_env())
        badge_results=json.loads(next(line for line in badge_output.splitlines() if line.startswith('{')))
        validate_badge_alpha(badge_results)
        (work/'badge-alpha-results.json').write_text(json.dumps(badge_results,indent=2)+'\n')
        frame_output=call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.UiAdapterTests','Deception.Tests.FrameTests','Run'],log=log,env=clean_env())
        frame_results=json.loads(next(line for line in frame_output.splitlines() if line.startswith('{')))
        validate_map_frame(frame_results)
        (work/'map-frame-results.json').write_text(json.dumps(frame_results,indent=2)+'\n')
        naval_output=call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.UiAdapterTests','Deception.Tests.NavalPresentationTests','Run'],log=log,env=clean_env())
        naval_results=json.loads(next(line for line in naval_output.splitlines() if line.startswith('{')))
        validate_naval_presentation(naval_results)
        (work/'naval-presentation-results.json').write_text(json.dumps(naval_results,indent=2)+'\n')
        run_regroup_fixture(dotnet,compiler,work,refs,log,host,runtime,portrait_resource)
        run_regroup_animation(dotnet,compiler,work,refs,log,host,runtime)
        run_disguise_refresh(dotnet,compiler,work,refs,log,host,runtime)
        run_growing_disguises(dotnet,compiler,work,refs,log,host,runtime)
        run_disguise_lifecycle(dotnet,compiler,work,refs,log,host,runtime)
        run_mimic_transport(dotnet,compiler,work,refs,log,host,runtime)
        run_candidate_0359(dotnet,compiler,work,refs,log,host,runtime)
        run_online_fixtures(dotnet,compiler,work,refs,log,host,runtime)
        run_regroup_ledger(dotnet,compiler,work,refs,log,host,runtime)
        run_ledger_read(dotnet,compiler,work,refs,log,host,runtime)
        run_regroup_planning(dotnet,compiler,work,refs,log,host,runtime)
        run_registry_order(dotnet,compiler,work,refs,log,host,runtime)
        # Regression for native SelectUnit clearing selectedTile. Compile the
        # real selection guard with synthetic current unit/map identities;
        # this does not exercise Unity rendering or establish game success.
        intelligence_sources=core+[ROOT/'src/PolyMod'/n for n in
            ('CompilerMetadata.cs','NativeApi.cs','NativeIntelligenceOverlay.cs')]+[
            ROOT/'tests/intelligence/IntelligenceSelectionTests.cs',ROOT/'tests/intelligence/KnowledgeInactivePorts.cs',ROOT/'tests/intelligence/RogueInactivePorts.cs']
        csc(dotnet,compiler,intelligence_sources,work/'IntelligenceSelectionTests.dll',
            refs+[game/'BepInEx/core/0Harmony.dll'],log)
        env['DCP_INTELLIGENCE_SELECTION_RESULT']=str(work/'intelligence-selection-results.json')
        call(['/usr/bin/arch','-x86_64',host,runtime,work,'IntelligenceSelectionTests',
              'Deception.Tests.IntelligenceSelectionTests','Run'],log=log,env=env)
        intelligence=json.loads(regular(work/'intelligence-selection-results.json').read_text())
        if intelligence.get('passed')!=26 or intelligence.get('failed')!=0 or len(intelligence.get('results',[]))!=26:
            raise Stop('Intelligence selection guard regression checks failed.')
        train_sources=core+[ROOT/'src/PolyMod'/n for n in
            ('CompilerMetadata.cs','NativeApi.cs','NativeTrainStreamContext.cs','NativePaidBirthCommands.cs',
             'NativeTrainLeafProbeAdapter.cs','NativeTrainLeafProbeDriver.cs','NativeTrainLeafDispatcher.cs','NativeTrainFactoryTraversal.cs')]+sorted((ROOT/'tests/train-leaf').glob('*.cs'))
        csc(dotnet,compiler,train_sources,work/'TrainLeafTests.dll',refs+[game/'BepInEx/core/0Harmony.dll'],log)
        env['DCP_REGISTRY_FIXTURE']=str(ROOT/'tests/native-tree.captured.json')
        for name,owner in [('train-leaf','NativeTrainLeafProbeDraftTests'),('train-context','NativeTrainFactoryAdmissionDraftTests'),('train-dispatch','NativeTrainDispatchDraftTests')]:
            env['DECEPTION_TEST_OUTPUT']=str(work/(name+'-results.json'))
            call(['/usr/bin/arch','-x86_64',host,runtime,work,'TrainLeafTests','Deception.Tests.'+owner,'Run'],log=log,env=env)
            validate_train_leaf_results(name,json.loads(regular(work/(name+'-results.json')).read_text()))
        outer_sources=core+[ROOT/'src/PolyMod'/n for n in
            ('CompilerMetadata.cs','NativeApi.cs','NativeTrainStreamContext.cs','NativePaidBirthCommands.cs',
             'NativeTrainLeafProbeAdapter.cs','NativeTrainLeafDispatcher.cs','NativeTrainFactoryTraversal.cs',
             'NativeTrainFactoryHelperDriver.cs','NativeTrainCommandRecord.cs')]+sorted((ROOT/'tests/train-outer').glob('*.cs'))
        csc(dotnet,compiler,outer_sources,work/'TrainOuterTests.dll',refs+[game/'BepInEx/core/0Harmony.dll'],log)
        for name,owner in [('train-record','NativeTrainCommandRecordDraftTests'),('train-factory','NativeTrainFactoryTraversalDraftTests'),('train-outer-context','NativeTrainFactoryAdmissionDraftTests')]:
            env['DECEPTION_TEST_OUTPUT']=str(work/(name+'-results.json'))
            call(['/usr/bin/arch','-x86_64',host,runtime,work,'TrainOuterTests','Deception.Tests.'+owner,'Run'],log=log,env=env)
            validate_train_outer_results(name,json.loads(regular(work/(name+'-results.json')).read_text()))
        command_sources=core+[ROOT/'src/PolyMod'/n for n in
            ('CompilerMetadata.cs','NativeApi.cs','NativeTrainStreamContext.cs','NativePaidBirthCommands.cs',
             'NativeTrainLeafProbeAdapter.cs','NativeTrainLeafDispatcher.cs','NativeTrainFactoryTraversal.cs',
             'NativeTrainCommandRecord.cs','NativeCommandOuterAbi.cs','NativeCommandOuterAdapter.cs')]+sorted((ROOT/'tests/command-outer').glob('*.cs'))
        csc(dotnet,compiler,command_sources,work/'CommandOuterTests.dll',refs+[game/'BepInEx/core/0Harmony.dll'],log,unsafe=True)
        for name,owner in [('command-outer','NativeCommandOuterCallbackDraftTests'),('command-outer-context','NativeTrainFactoryAdmissionDraftTests'),('command-outer-record','NativeTrainCommandRecordDraftTests')]:
            env['DECEPTION_TEST_OUTPUT']=str(work/(name+'-results.json'))
            call(['/usr/bin/arch','-x86_64',host,runtime,work,'CommandOuterTests','Deception.Tests.'+owner,'Run'],log=log,env=env)
            validate_command_outer_results(name,json.loads(regular(work/(name+'-results.json')).read_text()))
        csc(dotnet,compiler,build_journal_sources(work),work/'BuildJournalTests.dll',refs+[game/'BepInEx/core/0Harmony.dll'],log)
        for name,owner in [('build-journal','NativeBuildJournalDraftTests'),('build-adapter','NativeBuildLeafAdapterTests'),('build-context','NativeTrainFactoryAdmissionDraftTests'),('attachment-retention','NativeAttachmentRetentionTests')]:
            env['DECEPTION_TEST_OUTPUT']=str(work/(name+'-results.json'))
            call(['/usr/bin/arch','-x86_64',host,runtime,work,'BuildJournalTests','Deception.Tests.'+owner,'Run'],log=log,env=env)
            validate_build_journal_results(name,json.loads(regular(work/(name+'-results.json')).read_text()))
        csc(dotnet,compiler,build_factory_sources(work),work/'BuildFactoryTests.dll',refs+[game/'BepInEx/core/0Harmony.dll'],log)
        env['DECEPTION_TEST_OUTPUT']=str(work/'build-factory-results.json')
        call(['/usr/bin/arch','-x86_64',host,runtime,work,'BuildFactoryTests','Deception.Tests.NativeBuildFactoryDraftTests','Run'],log=log,env=env)
        validate_build_factory_results('build-factory',json.loads(regular(work/'build-factory-results.json').read_text()))
        census_sources=[ROOT/'src/PolyMod'/n for n in ('CompilerMetadata.cs','NativeApi.cs','NativeProtectedCensus.cs')]+[
            ROOT/'tests/protected-census/NativeProtectedCensusTests.cs']
        csc(dotnet,compiler,census_sources,work/'ProtectedCensusTests.dll',refs,log)
        env['DECEPTION_TEST_OUTPUT']=str(work/'protected-census-results.json')
        call(['/usr/bin/arch','-x86_64',host,runtime,work,'ProtectedCensusTests','Deception.Tests.NativeProtectedCensusDraftTests','Run'],log=log,env=env)
        census=json.loads(regular(work/'protected-census-results.json').read_text())
        expected=json.loads(regular(ROOT/'tests/protected-census/expected.json').read_text())
        if census.get('passed')!=len(expected) or census.get('failed')!=0 or len(census.get('results',[]))!=len(expected) or {r.get('id') for r in census['results']}!=set(expected) or any(r.get('status')!='PASS' for r in census['results']):
            raise Stop('Protected native census conversion checks failed.')
        # Compile only our embedded native boundary using the existing local
        # compiler. No framework dependency, download, or external extraction.
        bridge=work/'libdeception-state-bridge.dylib'
        call(['/usr/bin/clang++','-arch','x86_64','-std=c++17','-dynamiclib','-O2','-fvisibility=hidden',
              '-Wall','-Wextra','-Werror','-mmacosx-version-min=11.0',
              '-Wl,-install_name,@rpath/libdeception-state-bridge.dylib',ROOT/'src/Native/deception-state-bridge.cpp','-o',bridge],log=log)
        call(['/usr/bin/lipo',bridge,'-verify_arch','x86_64'],log=log)
        exported=subprocess.check_output(['/usr/bin/nm','-gU',str(bridge)],text=True)
        names={line.split()[-1] for line in exported.splitlines() if line.strip()}
        expected={'_dcp_state_bridge_'+suffix for suffix in ('abi_version','config_size','call_size','plan_size','site_count','configure','configure_site','serialize','deserialize','command_serialize','command_deserialize','action_serialize','action_deserialize','train_command_serialize','train_command_deserialize','train_action_serialize','train_action_deserialize')}
        if names!=expected:raise Stop('Unexpected own-mod bridge exports.')
        harness=work/'native-bridge-harness'
        call(['/usr/bin/clang++','-arch','x86_64','-std=c++17','-Wall','-Wextra','-Werror','-fvisibility=hidden',
              ROOT/'tests/native/state-bridge-harness.cpp','-L'+str(work),'-ldeception-state-bridge',
              '-Wl,-rpath,@loader_path','-o',harness],log=log)
        with (work/'native-bridge-results.txt').open('w') as bridge_log:
            call(['/usr/bin/arch','-x86_64',harness],log=bridge_log)
        if (work/'native-bridge-results.txt').read_text().splitlines()[-1]!='RESULT 67 passed, 0 failed. C++ control flow only; IL2CPP integration remains untested.':
            raise Stop('Native bridge control-flow harness did not report all required cases.')
        bridge_digest=work/'state-bridge.sha256';bridge_digest.write_text(sha(bridge)+'\n')
        bridge_resources=[(bridge,'Deception.StateBridge.dylib'),(bridge_digest,'Deception.StateBridge.sha256'),
                          (ROOT/'tools/native-state-seams.json','Deception.StateBridge.seams.json')]
        platform_sources=[ROOT/'src/PolyMod'/n for n in ('NativePlatformContract.cs','NativePlatformLibrary.cs')]+sorted((ROOT/'tests/portability-0378').glob('*.cs'))
        csc(dotnet,compiler,platform_sources,work/'Deception.PlatformTests.dll',refs,log,resources=bridge_resources[:2])
        platform_output=call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.PlatformTests','Deception.Tests.PlatformTests','Run'],log=log,
            env={**clean_env(),'DECEPTION_PLATFORM_TEST_ROOT':str(work/'platform-artifacts'),'DECEPTION_GAME_ROOT':str(game)})
        platform_result=json.loads(next(line for line in platform_output.splitlines() if line.startswith('{')))
        validate_candidate_0359(platform_result,'portability-0378')
        (work/'portability-results.json').write_text(json.dumps(platform_result,indent=2)+'\n')
        execution=work/'libdeception-execution-bridge.dylib'
        call(['/usr/bin/clang++','-arch','x86_64','-std=c++17','-dynamiclib','-O2','-fvisibility=hidden',
              '-Wall','-Wextra','-Werror','-pedantic','-pthread','-mmacosx-version-min=11.0',
              '-Wl,-install_name,@rpath/libdeception-execution-bridge.dylib',ROOT/'src/Native/deception-execution-bridge.cpp','-o',execution],log=log)
        call(['/usr/bin/lipo',execution,'-verify_arch','x86_64'],log=log)
        exports=subprocess.check_output(['/usr/bin/nm','-gU',str(execution)],text=True)
        if {line.split()[-1] for line in exports.splitlines() if line.strip()}!={'_dcp_exec_bridge_'+suffix for suffix in
                ('abi_version','call_size','plan_size','config_size','site_count','configure_site','build_command','build_action','train_command','train_action')}:
            raise Stop('Unexpected own-mod execution bridge exports.')
        dependencies=subprocess.check_output(['/usr/bin/otool','-L',str(execution)],text=True)
        (work/'execution-bridge-dependencies.txt').write_text(dependencies)
        allowed={'@rpath/libdeception-execution-bridge.dylib','/usr/lib/libc++.1.dylib','/usr/lib/libSystem.B.dylib'}
        if {line.strip().split(' (',1)[0] for line in dependencies.splitlines()[1:]}!=allowed:
            raise Stop('Unexpected own-mod execution bridge dependency.')
        execution_harness=work/'execution-bridge-harness'
        call(['/usr/bin/clang++','-arch','x86_64','-std=c++17','-Wall','-Wextra','-Werror','-pedantic','-pthread','-fvisibility=hidden',
              ROOT/'tests/native/execution-bridge-harness.cpp','-L'+str(work),'-ldeception-execution-bridge',
              '-Wl,-rpath,@loader_path','-o',execution_harness],log=log)
        with (work/'execution-bridge-results.txt').open('w') as output:
            call(['/usr/bin/arch','-x86_64',execution_harness],log=output)
        if (work/'execution-bridge-results.txt').read_text().splitlines()[-1]!='PASS 85 native execution bridge control-flow assertions; mock native functions only.':
            raise Stop('Execution bridge control-flow checks failed.')
        callback_sources=[ROOT/'src/PolyMod'/n for n in ('NativeExecutionBridge.cs','NativePlatformContract.cs','NativePlatformLibrary.cs')]+sorted((ROOT/'tests/execution-callback').glob('*.cs'))
        csc(dotnet,compiler,callback_sources,work/'ExecutionCallbackTests.dll',refs+[game/'BepInEx/core/0Harmony.dll'],log,unsafe=True)
        env['DCP_EXEC_CALLBACK_RESULT']=str(work/'execution-callback-results.json')
        call(['/usr/bin/arch','-x86_64',host,runtime,work,'ExecutionCallbackTests','Deception.ExecutionCallbackTests.Harness','Run'],log=log,env=env)
        callback=json.loads(regular(work/'execution-callback-results.json').read_text())
        if callback.get('tests')!=36 or callback.get('failed')!=0 or callback.get('assertions')!=1103 or callback.get('activeOperations')!=0:
            raise Stop('Managed execution callback checks failed.')
        # Actual observer/claim production source with explicit fake native
        # boundaries. These never load IL2CPP or establish game acceptance.
        paid_suites=[
            ('PaidObservationMocks','Deception.PaidObservationMocks.Harness','DCP_PAID_OBSERVATION_RESULT','paid-observation-results.json',28,46,
             core+[ROOT/'src/PolyMod/NativePaidBirthObservation.cs',ROOT/'tests/paid-birth/PaidBirthObservationMocks.cs']),
            ('PrivateClaimMocks','Deception.PrivateClaimMocks.Harness','DCP_PRIVATE_CLAIM_RESULT','private-claim-results.json',16,79,
             core+[ROOT/'src/PolyMod/NativePrivateLedgerClaim.cs',ROOT/'src/PolyMod/NativeGameStateEnvelope.cs',ROOT/'src/PolyMod/NativeGameStateSnapshot.cs',
              ROOT/'tests/paid-birth/PrivateLedgerClaimMocks.cs'])]
        for assembly,entry,variable,result_file,count,assertions,sources in paid_suites:
            csc(dotnet,compiler,sources+[ROOT/'src/PolyMod/CompilerMetadata.cs'],work/(assembly+'.dll'),refs,log)
            env[variable]=str(work/result_file)
            call(['/usr/bin/arch','-x86_64',host,runtime,work,assembly,entry,'Run'],log=log,env=env)
            result=json.loads(regular(work/result_file).read_text())
            if result.get('tests')!=count or result.get('assertions')!=assertions or result.get('failed')!=0:
                raise Stop('Paid-birth observer/private-claim mock checks failed: '+assembly)
        execution_digest=work/'execution-bridge.sha256';execution_digest.write_text(sha(execution)+'\n')
        bridge_resources += [(execution,'Deception.ExecutionBridge.dylib'),(execution_digest,'Deception.ExecutionBridge.sha256'),
                            (ROOT/'tools/native-execution-seams.json','Deception.ExecutionBridge.seams.json')]
        command_bridge=work/'libdeception-command-bridge.dylib'
        call(['/usr/bin/clang++','-arch','x86_64','-std=c++17','-dynamiclib','-O2','-fvisibility=hidden',
              '-Wall','-Wextra','-Werror','-mmacosx-version-min=11.0',
              '-Wl,-install_name,@rpath/libdeception-command-bridge.dylib',ROOT/'src/Native/deception-command-bridge.cpp','-o',command_bridge],log=log)
        call(['/usr/bin/lipo',command_bridge,'-verify_arch','x86_64'],log=log)
        exports=subprocess.check_output(['/usr/bin/nm','-gU',str(command_bridge)],text=True)
        if {line.split()[-1] for line in exports.splitlines() if line.strip()}!={'_dcp_cmd_bridge_'+suffix for suffix in
                ('abi_version','call_size','value_size','plan_size','config_size','configure','to_bytes','from_bytes')}:
            raise Stop('Unexpected typed command bridge exports.')
        dependencies=subprocess.check_output(['/usr/bin/otool','-L',str(command_bridge)],text=True)
        (work/'command-bridge-dependencies.txt').write_text(dependencies)
        if {line.strip().split(' (',1)[0] for line in dependencies.splitlines()[1:]}!={'@rpath/libdeception-command-bridge.dylib','/usr/lib/libc++.1.dylib','/usr/lib/libSystem.B.dylib'}:
            raise Stop('Unexpected typed command bridge dependency.')
        command_harness=work/'command-bridge-harness'
        call(['/usr/bin/clang++','-arch','x86_64','-std=c++17','-Wall','-Wextra','-Werror','-fvisibility=hidden',
              ROOT/'tests/native/command-bridge-harness.cpp','-L'+str(work),'-ldeception-command-bridge',
              '-Wl,-rpath,@loader_path','-o',command_harness],log=log)
        with (work/'command-bridge-results.txt').open('w') as output:
            call(['/usr/bin/arch','-x86_64',command_harness],log=output)
        if (work/'command-bridge-results.txt').read_text().splitlines()[-1]!='PASS 117 typed command bridge checks':
            raise Stop('Typed command bridge control-flow checks failed.')
        command_digest=work/'command-bridge.sha256';command_digest.write_text(sha(command_bridge)+'\n')
        bridge_resources += [(command_bridge,'Deception.CommandBridge.dylib'),(command_digest,'Deception.CommandBridge.sha256'),
                            (ROOT/'tools/native-command-outer-seams.json','Deception.CommandBridge.seams.json')]
        adapter=sorted((ROOT/'src/PolyMod').glob('*.cs'))
        csc(dotnet,compiler,core+adapter+[ROOT/'src/Version.cs'],work/'Deception.dll',
            refs+[game/'BepInEx/core/BepInEx.Core.dll',game/'BepInEx/core/0Harmony.dll',
                  game/'BepInEx/core/Il2CppInterop.Runtime.dll',game/'BepInEx/interop/Il2Cppmscorlib.dll',
                  game/'BepInEx/interop/GameLogicAssembly.dll',game/'BepInEx/interop/PolytopiaAssembly.dll',
                  game/'BepInEx/interop/UnityEngine.CoreModule.dll'],log,resources=bridge_resources)
        call([sys.executable,ROOT/'tools/audit_compiler_metadata.py',work/'Deception.dll',work/'compiler-metadata-results.json'],log=log)
        make_archive(work/'Deception.dll',work/MODNAME)
    if source!=same_tree_sources():raise Stop('Project source changed during the build; install disabled.')
    if before!=preflight(game):raise Stop('Framework/config changed during build; install disabled.')
    receipt={'status':'MODEL_TESTS_PASSED_NATIVE_NOT_RUN','work':str(work),'sources':source,
             'framework_and_config':before,'artifact':str(work/MODNAME),'artifact_sha256':sha(work/MODNAME),
             'native_bridge_results_sha256':sha(work/'native-bridge-results.txt'),'native_bridge_sha256':sha(bridge),'dll_sha256':sha(work/'Deception.dll'),'model_results_sha256':sha(work/'model-test-results.json'),
             'execution_bridge_sha256':sha(execution),'execution_bridge_results_sha256':sha(work/'execution-bridge-results.txt'),
             'execution_callback_results_sha256':sha(work/'execution-callback-results.json'),
             'paid_observation_results_sha256':sha(work/'paid-observation-results.json'),
             'private_claim_results_sha256':sha(work/'private-claim-results.json'),
             **{key.replace('-','_')+'_results_sha256':sha(work/(key+'-results.json')) for key in ('lighthouse-secret','elyrion-eggs','online-protocol','online-session','online-preview','online-dispatch','online-resign','online-maintenance','disband-presentation','completed-sight','rogue-combat','portability','ordinary-actions','spy-sizing','path-preference','tech-preload','lighthouse','shed-presentation','aquarion-art','flood-transport','mimic-movement','spy-effects','rogue-visuals','blackmail-embassy-ui','blackmail-hostility','blackmail-notices','blackmail-income','blackmail','blackmail-presentation','shed-water','disguise-movement','exchange-transformation','combat-preview','spy-readiness','action-bar','mimic-water','apparent-splash','presentation','coastal-recruitment','cloak-readiness','knowledge','menu-preview','shed-disguise','special-ui','feign-support','blackout','blackout-connections','fogger-actions','technology-intelligence','fogger-art','apparent-terrain','rogue','rogue-sight','rogue-upgrade','cloak-render','hotseat-session')},
             'ui_adapter_results_sha256':sha(work/'ui-adapter-results.json'),
             'badge_alpha_results_sha256':sha(work/'badge-alpha-results.json'),
             'map_frame_results_sha256':sha(work/'map-frame-results.json'),
             'naval_presentation_results_sha256':sha(work/'naval-presentation-results.json'),
             'disguise_refresh_results_sha256':sha(work/'disguise-refresh-results.json'),
             'disguise_growth_results_sha256':sha(work/'disguise-growth-results.json'),
             'growth_audio_results_sha256':sha(work/'growth-audio-results.json'),
             'disguise_lifecycle_results_sha256':sha(work/'disguise-lifecycle-results.json'),
             'mimic_transport_results_sha256':sha(work/'mimic-transport-results.json'),
             'hook_abi_results_sha256':sha(work/'hook-abi-results.json'),
             'cloak_movement_results_sha256':sha(work/'cloak-movement-results.json'),
             'concealment_results_sha256':sha(work/'concealment-results.json'),
             'apparent_upgrades_results_sha256':sha(work/'apparent-upgrades-results.json'),
             'mimic_prefab_results_sha256':sha(work/'mimic-prefab-results.json'),
             'growth_sight_results_sha256':sha(work/'growth-sight-results.json'),
             'regroup_fixture_results_sha256':sha(work/'regroup-fixture-results.json'),
             'regroup_animation_results_sha256':sha(work/'regroup-animation-results.json'),
             'regroup_ledger_results_sha256':sha(work/'regroup-ledger-results.json'),
             'ledger_read_results_sha256':sha(work/'ledger-read-results.json'),
             'regroup_planning_results_sha256':sha(work/'regroup-planning-results.json'),
             'registry_order_results_sha256':sha(work/'registry-order-results.json'),
             'intelligence_selection_results_sha256':sha(work/'intelligence-selection-results.json'),
             'train_leaf_results_sha256':{name:sha(work/(name+'-results.json')) for name in ('train-leaf','train-context','train-dispatch')},
             'train_outer_results_sha256':{name:sha(work/(name+'-results.json')) for name in ('train-record','train-factory','train-outer-context')},
             'command_bridge_sha256':sha(command_bridge),'command_bridge_results_sha256':sha(work/'command-bridge-results.txt'),
             'command_outer_results_sha256':{name:sha(work/(name+'-results.json')) for name in ('command-outer','command-outer-context','command-outer-record')},
             'build_journal_results_sha256':{name:sha(work/(name+'-results.json')) for name in ('build-journal','build-adapter','build-context','attachment-retention')},
             'build_factory_results_sha256':sha(work/'build-factory-results.json'),
             'protected_census_results_sha256':sha(work/'protected-census-results.json'),
             'compiler_metadata_results_sha256':sha(work/'compiler-metadata-results.json'),
             'native_adapter':'NOT RUN','date':dt.datetime.now().astimezone().isoformat()}
    (work/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
    pointer=ROOT/'build/latest.json';temp=pointer.with_suffix('.tmp');temp.write_text(json.dumps(receipt,indent=2)+'\n');os.replace(temp,pointer)
    print('%s BUILD READY: %d model tests and %d UI-adapter fixture checks passed; adapter compiled; nothing installed.'%(MILESTONE,len(expected_model_ids()),len(expected_ui_adapter_ids())),flush=True)
    print('Full gameplay acceptance (including A01) remains NOT RUN. Next: bash run.sh install',flush=True)

def active_entries(game):
    mods=game/'Mods'
    if not mods.is_dir():return []
    if mods.is_symlink():raise Stop('Mods directory is a symlink; automatic install refused.')
    # All non-dot entries, including logs/old directories, are reported. Never move them automatically.
    return [p for p in mods.iterdir() if not p.name.startswith('.') and p.name!=MODNAME]

def install_archive(artifact,destination,backup_root,accepted_previous=None,backup_previous=True):
    """Install only our content archive, recognizing exact prior versions; backup is caller-selectable.
    Caller has closed Polytopia and acquired the package's operation lock.
    This is not a guarantee against a hostile concurrent external file writer.
    """
    regular(artifact);data=artifact.read_bytes();wanted=digest(data)
    destination.parent.mkdir(exist_ok=True)
    if destination.parent.is_symlink() or destination.is_symlink():raise Stop('Refusing a mod path symlink.')
    previous=None;previous_sha=None;backup=None
    if destination.exists():
        previous=regular(destination).read_bytes();previous_sha=digest(previous)
        if previous_sha==wanted:return 'ALREADY INSTALLED'
        if previous_sha!=PREVIOUS_MOD_SHA256 and previous_sha not in (accepted_previous or set()):
            raise Stop('Existing Deception archive was not verified as the known prior build. Nothing replaced. Preserve it in place; inspect and recognize its exact prior hash before retrying. Do not move/delete it to evade this guard or change any framework library.')
        if backup_previous:
            backup_root.mkdir(parents=True,exist_ok=True)
            if backup_root.is_symlink():raise Stop('Refusing a backup-directory symlink.')
            label='v0.1.0' if previous_sha==PREVIOUS_MOD_SHA256 else 'before-'+MILESTONE
            backup=backup_root/('alex_deception_m0-'+label+'-'+previous_sha[:16]+'.polymod')
            if backup.is_symlink():raise Stop('Refusing a backup symlink.')
            if backup.exists():
                if digest(regular(backup).read_bytes())!=previous_sha:raise Stop('Existing backup does not match the original; nothing replaced.')
            else:
                with backup.open('xb') as f:f.write(previous);f.flush();os.fsync(f.fileno())
            if sha(backup)!=previous_sha:raise Stop('Backup verification failed; nothing replaced.')
    fd,temp=tempfile.mkstemp(prefix='.deception-install-',dir=str(destination.parent));tmp=Path(temp);replaced=False
    try:
        with os.fdopen(fd,'wb') as f:f.write(data);f.flush();os.fsync(f.fileno())
        if sha(tmp)!=wanted:raise Stop('Staged archive failed checksum.')
        if previous is None:
            os.link(tmp,destination) # Exclusive creation for a clean installation.
        else:
            if destination.is_symlink() or sha(regular(destination))!=previous_sha:
                raise Stop('Installed archive changed during preparation; replacement cancelled.')
            os.replace(tmp,destination);replaced=True
        if sha(regular(destination))!=wanted:raise Stop('Installed archive checksum mismatch; do not launch.')
    except Exception:
        # If a failure is after our successful rename, restore only if the file
        # still contains our own candidate. Never overwrite an unrelated writer.
        if replaced and previous is not None and destination.is_file() and not destination.is_symlink() and sha(destination)==wanted:
            fd,rollback_name=tempfile.mkstemp(prefix='.deception-rollback-',dir=str(destination.parent));rollback=Path(rollback_name)
            try:
                with os.fdopen(fd,'wb') as f:f.write(previous);f.flush();os.fsync(f.fileno())
                if sha(rollback)!=previous_sha:raise Stop('Rollback staging failed; inspect the install error before launching.')
                os.replace(rollback,destination)
            finally:rollback.unlink(missing_ok=True)
        raise
    finally:tmp.unlink(missing_ok=True)
    if backup:print('PREVIOUS MOD BACKUP: '+str(backup),flush=True)
    return 'UPGRADED' if previous is not None else 'INSTALLED'

def previous_receipt_hashes(game,home=None):
    """Accept a locally built M0.2 only when its receipt matches the supplied prior source tree.
    This is file-conflict protection, not a cryptographic signature or a native execution verdict.
    Old code is never imported or executed.
    """
    home=Path.home() if home is None else home
    contract=json.loads(regular(ROOT/'tools/previous-source-contract.json').read_text())
    folder=home/'Downloads'/contract['package']
    receipt_path=folder/'build/latest.json'
    if not receipt_path.is_file() or receipt_path.is_symlink():return set()
    try:
        old=json.loads(regular(receipt_path).read_text())
        if old.get('status')!='MODEL_TESTS_PASSED_NATIVE_NOT_RUN' or old.get('sources')!=contract['sources']:return set()
        actual={str(k):v for k,v in old.get('framework_and_config',{}).items()}
        if set(actual)!=(set(CONTRACT['framework_checksums'])|{'BepInEx/config/BepInEx.cfg'}):return set()
        # An old runtime/config checkpoint must match its own receipt's current source files.
        if any(sha(regular(game/k))!=v for k,v in actual.items()):return set()
        installed=game/'Mods'/MODNAME
        if not installed.exists():return set()
        value=sha(regular(installed))
        if value!=old.get('artifact_sha256'):return set()
        work=Path(old['work'])
        if work.parent.resolve()!=(folder/'build').resolve():return set()
        result=regular(work/'model-test-results.json')
        if sha(result)!=old.get('model_results_sha256'):return set()
        report=json.loads(result.read_text());ids=contract['expected_tests'];records=report.get('tests',[])
        if report.get('is64Bit') is not True or report.get('total')!=len(ids) or report.get('passed')!=len(ids) or report.get('failed')!=0:return set()
        if len(records)!=len(ids) or {r.get('id') for r in records}!=set(ids) or any(r.get('status')!='PASS' for r in records):return set()
        with zipfile.ZipFile(installed) as z:
            if z.testzip() is not None or set(z.namelist())!={'manifest.json','localization.json','Deception.dll','DEVELOPMENT-STATUS.txt'}:return set()
            mf=json.loads(z.read('manifest.json'))
            if mf.get('id')!='alex_deception' or mf.get('version')!=contract['version']:return set()
            if digest(z.read('Deception.dll'))!=old.get('dll_sha256'):return set()
        return {value}
    except (KeyError,ValueError,TypeError,OSError,Stop,zipfile.BadZipFile):return set()

def install(game,backup_previous=True):
    before=preflight(game);receipt=json.loads(regular(ROOT/'build/latest.json').read_text())
    if receipt.get('status')!='MODEL_TESTS_PASSED_NATIVE_NOT_RUN' or receipt['sources']!=same_tree_sources():raise Stop('Build missing/stale. Re-run build first.')
    if receipt['framework_and_config']!=before:raise Stop('Installation differs from the build inputs.')
    artifact=Path(receipt['artifact'])
    if sha(regular(artifact))!=receipt['artifact_sha256']:raise Stop('Built content archive changed.')
    res=Path(receipt['work'])/'model-test-results.json'
    if sha(regular(res))!=receipt['model_results_sha256']:raise Stop('Model test evidence changed.')
    validate_model_results(json.loads(res.read_text()))
    ui_res=Path(receipt['work'])/'ui-adapter-results.json'
    if sha(regular(ui_res))!=receipt.get('ui_adapter_results_sha256'):raise Stop('UI-adapter test evidence missing or changed.')
    validate_ui_adapter_results(json.loads(ui_res.read_text()))
    badge=Path(receipt['work'])/'badge-alpha-results.json'
    if sha(regular(badge))!=receipt.get('badge_alpha_results_sha256'):raise Stop('Native badge alpha evidence missing or changed.')
    validate_badge_alpha(json.loads(badge.read_text()))
    frame=Path(receipt['work'])/'map-frame-results.json'
    if sha(regular(frame))!=receipt.get('map_frame_results_sha256'):raise Stop('Warrior frame evidence missing or changed.')
    validate_map_frame(json.loads(frame.read_text()))
    for key,validator in (('naval_presentation',validate_naval_presentation),('mimic_transport',validate_mimic_transport),('disguise_lifecycle',validate_disguise_lifecycle)):
        result_file=Path(receipt['work'])/(key.replace('_','-')+'-results.json')
        if sha(regular(result_file))!=receipt.get(key+'_results_sha256'):raise Stop('Naval/disguise fixture evidence missing or changed: '+key)
        validator(json.loads(result_file.read_text()))
    for key,folder in (('lighthouse-secret','lighthouse-secret-0398'),('elyrion-eggs','elyrion-eggs-0396'),('online-protocol','online-sync'),('online-session','online-session-0383'),('online-preview','online-preview'),('online-dispatch','online-dispatch'),('online-resign','online-resign'),('online-maintenance','online-maintenance'),('disband-presentation','disband-presentation-0380'),('completed-sight','rogue-sight-0378'),('rogue-combat','rogue-combat-0378'),('portability','portability-0378'),('ordinary-actions','ordinary-actions-0377'),('spy-sizing','spy-sizing-0377'),('path-preference','path-preference-0375'),('tech-preload','tech-preload-0375'),('lighthouse','blackout-lighthouse-0373'),('shed-presentation','shed-presentation-0373'),('aquarion-art','aquarion-art-0371'),('flood-transport','flood-transport-0371'),('mimic-movement','mimic-movement-0370'),('spy-effects','spy-effects-0370'),('rogue-visuals','rogue-visuals-0370'),('blackmail-embassy-ui','blackmail-embassy-ui-0369'),('blackmail-hostility','blackmail-hostility-0369'),('blackmail-notices','blackmail-notices-0369'),('blackmail-income','blackmail-income-0369'),('blackmail','blackmail-0368'),('blackmail-presentation','blackmail-presentation-0368'),('shed-water','shed-water-0368'),('disguise-movement','disguise-movement-0367'),('exchange-transformation','exchange-transformation-0367'),('combat-preview','combat-preview-0367'),('spy-readiness','spy-readiness-0367'),('action-bar','action-bar-0366'),('mimic-water','mimic-water-0366'),('apparent-splash','apparent-splash-0366'),('hotseat-session','session-0363'),('cloak-render','cloak-render-0363'),('rogue-upgrade','rogue-upgrade-0362'),('rogue-sight','rogue-sight-0362'),('rogue','rogue-0362'),('special-ui','special-ui-0362'),('feign-support','feign-support-0362'),('blackout','blackout-0362'),('blackout-connections','blackout-connections-0393'),('fogger-actions','fogger-actions-0390'),('technology-intelligence','technology-intelligence-0390'),('fogger-art','fogger-art-0390'),('apparent-terrain','apparent-terrain-0362'),('shed-disguise','shed-disguise-0361'),('presentation','presentation-0361'),('coastal-recruitment','coastal-recruitment-0361'),('cloak-readiness','cloak-readiness-0361'),('knowledge','intelligence-0361'),('menu-preview','menu-preview-0361'),('hook-abi','hook-abi-0360'),('cloak-movement','cloak-movement-0359'),('concealment','concealment-0359'),('apparent-upgrades','apparent-upgrades-0359')):
        result_file=Path(receipt['work'])/(key+'-results.json')
        if sha(regular(result_file))!=receipt.get(key.replace('-','_')+'_results_sha256'):raise Stop('Cloak/apparent upgrade evidence missing or changed: '+key)
        validate_candidate_0359(json.loads(result_file.read_text()),folder)
    disguise=Path(receipt['work'])/'disguise-refresh-results.json'
    if sha(regular(disguise))!=receipt.get('disguise_refresh_results_sha256'):raise Stop('Disguise visual refresh evidence missing or changed.')
    validate_disguise_refresh(json.loads(disguise.read_text()))
    for key,validator in (('disguise_growth',validate_disguise_growth),('growth_audio',validate_growth_audio),('mimic_prefab',validate_mimic_prefab),('growth_sight',validate_growth_sight)):
        result_file=Path(receipt['work'])/(key.replace('_','-')+'-results.json')
        if sha(regular(result_file))!=receipt.get(key+'_results_sha256'):raise Stop('Growing disguise fixture evidence missing or changed: '+key)
        validator(json.loads(result_file.read_text()))
    regroup=Path(receipt['work'])/'regroup-fixture-results.json'
    if sha(regular(regroup))!=receipt.get('regroup_fixture_results_sha256'):raise Stop('Regroup fixture evidence missing or changed.')
    validate_regroup_results(json.loads(regroup.read_text()))
    animation=Path(receipt['work'])/'regroup-animation-results.json'
    if sha(regular(animation))!=receipt.get('regroup_animation_results_sha256'):raise Stop('Regroup animation fixture evidence changed.')
    validate_regroup_animation(json.loads(animation.read_text()))
    ledger=Path(receipt['work'])/'regroup-ledger-results.json'
    if sha(regular(ledger))!=receipt.get('regroup_ledger_results_sha256'):raise Stop('Regroup ledger fixture evidence changed.')
    validate_regroup_ledger(json.loads(ledger.read_text()))
    reads=Path(receipt['work'])/'ledger-read-results.json'
    if sha(regular(reads))!=receipt.get('ledger_read_results_sha256'):raise Stop('Ledger read fixture evidence changed.')
    validate_ledger_read(json.loads(reads.read_text()))
    planning=Path(receipt['work'])/'regroup-planning-results.json'
    if sha(regular(planning))!=receipt.get('regroup_planning_results_sha256'):raise Stop('Regroup planning fixture evidence changed.')
    validate_regroup_planning(json.loads(planning.read_text()))
    registry=Path(receipt['work'])/'registry-order-results.json'
    if sha(regular(registry))!=receipt.get('registry_order_results_sha256'):raise Stop('Registry order fixture evidence changed.')
    validate_registry_order(json.loads(registry.read_text()))
    intelligence=Path(receipt['work'])/'intelligence-selection-results.json'
    if sha(regular(intelligence))!=receipt.get('intelligence_selection_results_sha256'):raise Stop('Intelligence fixture evidence changed.')
    if sha(regular(Path(receipt['work'])/'protected-census-results.json'))!=receipt.get('protected_census_results_sha256'):raise Stop('Protected census fixture evidence changed.')
    for name in ('train-leaf','train-context','train-dispatch'):
        result=Path(receipt['work'])/(name+'-results.json')
        if sha(regular(result))!=receipt.get('train_leaf_results_sha256',{}).get(name):raise Stop('Train leaf fixture evidence changed.')
        validate_train_leaf_results(name,json.loads(result.read_text()))
    for name in ('train-record','train-factory','train-outer-context'):
        result=Path(receipt['work'])/(name+'-results.json')
        if sha(regular(result))!=receipt.get('train_outer_results_sha256',{}).get(name):raise Stop('Train outer fixture evidence changed.')
        validate_train_outer_results(name,json.loads(result.read_text()))
    for name in ('command-outer','command-outer-context','command-outer-record'):
        result=Path(receipt['work'])/(name+'-results.json')
        if sha(regular(result))!=receipt.get('command_outer_results_sha256',{}).get(name):raise Stop('Typed command fixture evidence changed.')
        validate_command_outer_results(name,json.loads(result.read_text()))
    for name in ('build-journal','build-adapter','build-context','attachment-retention'):
        result=Path(receipt['work'])/(name+'-results.json')
        if sha(regular(result))!=receipt.get('build_journal_results_sha256',{}).get(name):raise Stop('Build journal fixture evidence changed.')
        validate_build_journal_results(name,json.loads(result.read_text()))
    factory_result=Path(receipt['work'])/'build-factory-results.json'
    if sha(regular(factory_result))!=receipt.get('build_factory_results_sha256'):raise Stop('Build factory fixture evidence changed.')
    validate_build_factory_results('build-factory',json.loads(factory_result.read_text()))
    for file,key in [('libdeception-command-bridge.dylib','command_bridge_sha256'),('command-bridge-results.txt','command_bridge_results_sha256')]:
        if sha(regular(Path(receipt['work'])/file))!=receipt.get(key):raise Stop('Typed command bridge evidence changed.')
    bridge=Path(receipt['work'])/'libdeception-state-bridge.dylib'
    bridge_result=Path(receipt['work'])/'native-bridge-results.txt'
    if sha(regular(bridge))!=receipt.get('native_bridge_sha256') or sha(regular(bridge_result))!=receipt.get('native_bridge_results_sha256'):
        raise Stop('Own-mod native bridge or control-flow evidence changed.')
    for file,key in [('libdeception-execution-bridge.dylib','execution_bridge_sha256'),('execution-bridge-results.txt','execution_bridge_results_sha256'),
                     ('execution-callback-results.json','execution_callback_results_sha256'),
                     ('paid-observation-results.json','paid_observation_results_sha256'),
                     ('private-claim-results.json','private_claim_results_sha256')]:
        if sha(regular(Path(receipt['work'])/file))!=receipt.get(key):raise Stop('Execution bridge or callback evidence changed.')
    metadata=Path(receipt['work'])/'compiler-metadata-results.json'
    if sha(regular(metadata))!=receipt.get('compiler_metadata_results_sha256'):raise Stop('Compiler metadata evidence missing or changed.')
    compiler_result=json.loads(metadata.read_text())
    if compiler_result.get('status')!='MANAGED_NULLABLE_METADATA_MATCH' or compiler_result.get('dll_sha256')!=receipt['dll_sha256']:
        raise Stop('Compiler metadata does not match the built adapter.')
    others=active_entries(game)
    if others:raise Stop('Move community mods/debug clutter OUTSIDE Mods first (including Polibrary): '+', '.join(p.name for p in others))
    status=install_archive(artifact,game/'Mods'/MODNAME,game/'Disabled Mods/Deception backups',previous_receipt_hashes(game)|{OBSERVED_M1A_SHA256,OBSERVED_M1B_SHA256,OBSERVED_M1C_SHA256,OBSERVED_M1D_R1_SHA256,OBSERVED_M1E_SHA256,OBSERVED_M1E_DIAGNOSTIC_026_SHA256,OBSERVED_M1E_REPAIR_027_SHA256,OBSERVED_M1E_REPAIR_028_SHA256,OBSERVED_M1E_REPAIR_029_SHA256,OBSERVED_M1E_REPAIR_0210_SHA256,OBSERVED_M1E_REPAIR_0211_SHA256,OBSERVED_SESSION_MAP_0212_SHA256,OBSERVED_SESSION_MAP_0213_SHA256,OBSERVED_SESSION_MAP_0214_SHA256,OBSERVED_SESSION_MAP_0215_SHA256,OBSERVED_SESSION_MAP_0216_SHA256,OBSERVED_SMUGGLE_030_SHA256,OBSERVED_SMUGGLE_031_SHA256,OBSERVED_SMUGGLE_032_SHA256,OBSERVED_SMUGGLE_033_SHA256,OBSERVED_SMUGGLE_034_SHA256,OBSERVED_NATIVE_PROBE_035_SHA256,OBSERVED_NATIVE_ENVELOPE_036_SHA256,OBSERVED_NATIVE_BRIDGE_037_SHA256,OBSERVED_NATIVE_LEDGER_038_SHA256,OBSERVED_COMMAND_PROBE_039_SHA256,OBSERVED_COMMAND_PROBE_0310_SHA256,OBSERVED_EXCHANGE_PROBE_0311_SHA256,OBSERVED_REGISTRY_EXCHANGE_0312_SHA256,OBSERVED_EXCHANGE_SIGHT_0313_SHA256,OBSERVED_EXCHANGE_CHILDREN_0314_SHA256,OBSERVED_NATIVE_OWNERSHIP_0315_SHA256,OBSERVED_NATIVE_SESSION_0316_SHA256,OBSERVED_NATIVE_SESSION_0317_SHA256,OBSERVED_NATIVE_EXECUTION_0318_SHA256,OBSERVED_NATIVE_EXECUTION_0319_SHA256,OBSERVED_NATIVE_EXECUTION_0320_SHA256,OBSERVED_NATIVE_EXECUTION_0321_SHA256,OBSERVED_NATIVE_EXECUTION_0322_SHA256,OBSERVED_NATIVE_EXECUTION_0323_SHA256,OBSERVED_NATIVE_PAID_BIRTH_0324_SHA256,OBSERVED_A11_0325_SHA256,OBSERVED_A11_0326_SHA256,OBSERVED_A11_0327_SHA256,OBSERVED_A11_0328_SHA256,OBSERVED_A11_0329_SHA256,OBSERVED_TRAIN_0330_SHA256,OBSERVED_TRAIN_0331_SHA256,OBSERVED_TRAIN_0332_SHA256,OBSERVED_TRAIN_0333_SHA256,OBSERVED_COMMAND_0334_SHA256,OBSERVED_COMMAND_0335_SHA256,OBSERVED_COMMAND_0336_SHA256,OBSERVED_BUILD_0337_SHA256,OBSERVED_BUILD_FACTORY_0338_SHA256,OBSERVED_ART_BASELINE_0339_SHA256,OBSERVED_ART_0340_SHA256,OBSERVED_ART_0341_SHA256,OBSERVED_ART_0342_SHA256,OBSERVED_ART_0343_SHA256,OBSERVED_ART_0344_SHA256,OBSERVED_ART_0345_SHA256,OBSERVED_ART_0346_SHA256,OBSERVED_ART_0347_SHA256,OBSERVED_ART_0348_SHA256,OBSERVED_ART_0349_SHA256,OBSERVED_REGROUP_0350_SHA256,OBSERVED_REGROUP_0351_SHA256,OBSERVED_REGROUP_0352_SHA256,OBSERVED_REGROUP_0353_SHA256,OBSERVED_REGROUP_0354_SHA256,OBSERVED_GROWING_0355_SHA256,OBSERVED_ICONS_0356_SHA256,OBSERVED_NAVAL_0357_SHA256,OBSERVED_FEEDBACK_0358_SHA256,OBSERVED_CRASH_0359_SHA256,OBSERVED_REPAIR_0360_SHA256,OBSERVED_SPY_0361_SHA256,OBSERVED_ESPIONAGE_0362_SHA256,OBSERVED_HOTSEAT_0363_SHA256,OBSERVED_STARTUP_0364_SHA256,OBSERVED_SUPPORT_0365_SHA256,OBSERVED_OBSERVER_0366_SHA256,OBSERVED_BLACKMAIL_BASE_0367_SHA256,OBSERVED_BLACKMAIL_UI_0368_SHA256,OBSERVED_SPY_FEEDBACK_0369_SHA256,OBSERVED_AQUARION_0370_SHA256,OBSERVED_AQUARION_REPAIR_0371_SHA256,OBSERVED_SPYMAID_FAIRY_0372_SHA256,OBSERVED_SPY_DISGUISE_0373_SHA256,OBSERVED_SAVE_FOG_0374_SHA256,OBSERVED_PREVIEW_LOADING_0375_SHA256,OBSERVED_SPY_ACTIONS_0376_SHA256,OBSERVED_ROGUE_CRASH_0377_SHA256,OBSERVED_NATIVE_FOCUS_0378_SHA256,OBSERVED_CLOAK_0379_SHA256,OBSERVED_BADGE_0380_SHA256,OBSERVED_SPY_BALANCE_0381_SHA256,OBSERVED_ONLINE_BASELINE_0382_SHA256,OBSERVED_BLACKMAIL_LAYOUT_0383_SHA256,OBSERVED_BLACKMAIL_RELAY_0384_SHA256,OBSERVED_UNIT_BLACKMAIL_0385_SHA256,OBSERVED_SMUGGLE_SANCTUARY_0386_SHA256,OBSERVED_BLACKMAIL_COST_0387_SHA256,OBSERVED_SPY_STEALTH_0388_SHA256,OBSERVED_COUNTERINTELLIGENCE_0389_SHA256,OBSERVED_ART_UPDATER_0390_SHA256,OBSERVED_FEEDBACK_0391_SHA256,OBSERVED_TRADE_SECRETS_0392_SHA256,OBSERVED_FLAT_BLACKMAIL_0393_SHA256,OBSERVED_FOGGER_BADGE_0394_SHA256,OBSERVED_RESIGN_CRASH_0395_SHA256,OBSERVED_RELOAD_SAFETY_0396_SHA256,OBSERVED_BLACKOUT_DISCOVERY_0397_SHA256,OBSERVED_LIGHTHOUSE_SECRET_0398_SHA256,OBSERVED_SPY_MOVEMENT_0399_SHA256,OBSERVED_LIGHTHOUSE_REMOVAL_03100_SHA256,OBSERVED_BRANCH_POSITION_03101_SHA256},backup_previous=backup_previous)
    if before!=preflight(game):raise Stop('Framework/config changed while installing; no framework restore performed.')
    record={'status':status,'installed_path':str(game/'Mods'/MODNAME),'sha256':receipt['artifact_sha256'],
            'framework_unchanged':True,'native_game_tests':'NOT RUN','date':dt.datetime.now().astimezone().isoformat()}
    (ROOT/'build/install-receipts').mkdir(parents=True,exist_ok=True)
    (ROOT/'build/install-receipts/last-install.json').write_text(json.dumps(record,indent=2)+'\n')
    print(MILESTONE+' '+status+': only Mods/'+MODNAME+'. Framework, config and everyday launcher unchanged.')
    print(json.loads((ROOT/'content/manifest.json').read_text())['name']+'; native gameplay acceptance remains with Alex. Online peers require matching builds.')
    print('Launch with your existing Polytopia Modded shortcut. Leave the opening sequence untouched.')

def report(game):
    # Explicit report only. Reading reports never depends on a healthy game install.
    desktop=Path.home()/'Desktop'
    if not desktop.is_dir():raise Stop('Desktop directory missing.')
    fd,name=tempfile.mkstemp(prefix='deception-m0-report.',suffix='.zip',dir=str(desktop));os.close(fd)
    out=Path(name);members=[]
    def add(z,path,name):
        if path.is_file() and not path.is_symlink():z.write(path,name);members.append(name)
    with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as z:
        add(z,ROOT/'build/latest.json','build/latest.json')
        add(z,ROOT/'build/install-receipts/last-install.json','build/last-install.json')
        if (ROOT/'build').is_dir():
            for work in sorted((ROOT/'build').glob('m0-*'),key=lambda p:p.stat().st_mtime,reverse=True)[:1]:
                for n in ('build.log','model-test-results.json','ui-adapter-results.json','regroup-fixture-results.json','regroup-animation-results.json','regroup-ledger-results.json','native-api-results.json','compiler-metadata-results.json','receipt.json'):add(z,work/n,'build/'+n)
        evidence=Path.home()/'Library/Logs/Polytopia-Deception'
        for n in ('startup.json','native-registry-before.json','tree-registration.json','tree-after.json','recruitment-registration.json','events.jsonl','adapter-error.txt'):
            add(z,evidence/n,'deception/'+n)
        add(z,game/'BepInEx/LogOutput.log','game/LogOutput.log')
        add(z,game/'BepInEx/config/BepInEx.cfg','game/BepInEx.cfg')
        for n in ('session.json','launch.log','player.log'):
            add(z,Path.home()/'Library/Logs/Polytopia-Modded'/n,'daily-launcher/'+n)
        z.writestr('collection.json',json.dumps({'explicit_report':True,'milestone':MILESTONE,'installed_mod_sha256':sha(game/'Mods'/MODNAME) if (game/'Mods'/MODNAME).is_file() and not (game/'Mods'/MODNAME).is_symlink() else None,'files':members,'no_saves':True,
            'no_game_binaries':True,'note':'Logs include local paths. native-registry-before.json is static game data, not game state.'},indent=2))
    with zipfile.ZipFile(out) as z:
        if z.testzip() is not None:raise Stop('Report integrity failure.')
    print('REPORT READY: '+str(out))
    print('No Finder window opened. Report contains no saves, full native binaries, or framework DLL copies.')

@contextlib.contextmanager
def lock():
    # Stable lock is outside Mods; a terminated build leaves evidence and requires
    # reviewing/removing ONLY this project's lock. No process is killed.
    path=ROOT/'.operation-lock'
    try:path.mkdir()
    except FileExistsError:raise Stop('Another operation or stale lock exists: '+str(path))
    try:yield
    finally:
        # OneDrive can add READONLY to a newly created directory on Windows.
        # Remove only our empty lock, never its contents or any parent path.
        if os.name=='nt':
            import stat
            if path.stat().st_file_attributes & stat.FILE_ATTRIBUTE_READONLY:
                path.chmod(stat.S_IWRITE)
        path.rmdir()

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('mode',choices=('build','install','report','check','windows-check','windows-compile','windows-probe','windows-candidate','windows-tests'))
    parser.add_argument('--dotnet',type=Path,help='Existing SDK dotnet.exe for Windows compilation')
    parser.add_argument('--exception-probe',type=Path,help='Own compiled Windows diagnostic helper, only for windows-probe')
    parser.add_argument('--native-pack',type=Path)
    parser.add_argument('--mac-mod',type=Path)
    parser.add_argument('--probe-report',type=Path)
    parser.add_argument('--test-host',type=Path)
    parser.add_argument('--game',type=Path,default=GAME);parser.add_argument('--no-backup',action='store_true',help='Install without creating an old-mod backup');args=parser.parse_args();game=args.game.expanduser()
    if args.mode.startswith('windows-'):
        if args.game == GAME:raise Stop('Windows preparation requires an explicit --game installation path.')
        if args.mode=='windows-tests':
            import windows_tests
            with lock():windows_tests.run(sys.modules[__name__],game.resolve(),args.dotnet,args.test_host)
            return
        import windows_build
        with lock():windows_build.run(sys.modules[__name__],game,args.mode,args.dotnet,args.exception_probe,
                                     args.native_pack,args.mac_mod,args.probe_report)
        return
    if args.mode=='report':report(game);return
    with lock():
        if args.mode=='check':preflight(game);print('HOST CHECK PASSED: existing pinned game/framework/config. No changes made.')
        elif args.mode=='build':build(game)
        else:install(game,backup_previous=not args.no_backup)
if __name__=='__main__':
    try:main()
    except (Stop,OSError,ValueError,KeyError,zipfile.BadZipFile) as e:
        print('STOP: '+str(e),file=sys.stderr)
        print('No framework restore/rebuild, game launch, download or Finder activation was performed.',file=sys.stderr)
        print('To explicitly collect the latest build/game evidence: bash run.sh report',file=sys.stderr)
        sys.exit(1)
