"""Producer DTO for pinned interface/build evidence, without runtime inference.

Source selectors and frozen versions are useful facts. They do not prove that a
Soong name is an installed module, nor that a published hash describes the current
source. Consumers must retain the proof obligations instead of dropping them.
"""
import copy

def produce(source, linkage, hidl_hashes=()):
    required=('repository','revision','path','sha256')
    identity={key:source[key] for key in required}
    result=dict(contract='source-interface-evidence/1',source=identity,
                declared_module=None,interface_candidates=[],module_candidates=[],
                applicability=dict(unknown_reason='No verified interface-to-generated-hash or installed-module version mapping',
                    **{'x-raw':dict(source=identity)}),
                unknown_reasons=[],runtime_observation=False,
                os_authority_granted=False,client_authority_granted=False)
    if linkage.get('candidates',0)>1:
        result['unknown_reasons'].append('Ambiguous build-module source membership')
    if linkage.get('build_evidence'):
        build=linkage['build_evidence']
        if any(source[key]!=build[key] for key in ('repository','revision')):
            raise ValueError('Build and declaration must share repository/revision')
        result['declared_module']=dict(domain='soong-build-module',name=linkage['build_module_name'],
            kind=linkage['module_kind'],source=copy.deepcopy(build),
            frozen_dump_version=linkage.get('frozen_dump_version'),
            declared_versions=copy.deepcopy(linkage.get('declared_versions',[])))
        flags=linkage.get('declared_flags',{})
        apex=flags.get('apex_available')
        if isinstance(apex,list):
            for name in apex:
                if isinstance(name,str):
                    result['module_candidates'].append(dict(name=name,
                        domain='soong-apex-availability-selector',installed_identity_proven=False,
                        version_range=None,source=copy.deepcopy(build)))
        result['unknown_reasons'].append('Soong build/module/APEX selectors do not establish installed package identity or version range')
    else:
        result['unknown_reasons'].append('No unique declared build module linked')
    version=linkage.get('frozen_dump_version')
    for fact in linkage.get('published_hash_lines',[]):
        if any(source[key]!=fact['source'][key] for key in ('repository','revision')):
            raise ValueError('Hash and declaration must share repository/revision')
        result['interface_candidates'].append(dict(domain='stable-aidl-frozen-dump',
            version_literal=version,published_hash=fact['literal'],hash_format=fact['format'],
            descriptor=None,source=copy.deepcopy(fact['source']),line=fact['line'],
            generated_hash_selection_proven=False,source_schema_equivalence_proven=False))
    for fact in hidl_hashes:
        if any(source[key]!=fact['source'][key] for key in ('repository','revision')):
            raise ValueError('HIDL hash and declaration must share repository/revision')
        result['interface_candidates'].append(dict(domain='hidl-published-identifier',
            descriptor=fact['declared_identifier'],minimum_version=None,
            published_hash=fact['literal'],hash_format=fact['format'],
            source=copy.deepcopy(fact['source']),line=fact['line'],
            generated_hash_selection_proven=False,source_schema_equivalence_proven=False))
    if result['interface_candidates']:
        result['unknown_reasons'].append('Published hashes require compiler hash-selection and source-schema equivalence evidence before activation')
    else:
        result['unknown_reasons'].append('No published hash linked to this declaration')
    return result
