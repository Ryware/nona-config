Pod::Spec.new do |s|
  s.name = 'NonaClient'
  s.version = '0.1.0'
  s.summary = 'Remote configuration and feature flags for Swift applications.'
  s.description = 'Read frontend-scoped Nona configuration with defaults, offline caching, and explicit fetch/activate lifecycle.'
  s.homepage = 'https://nonaconfig.com'
  s.license = { :type => 'Apache-2.0', :file => 'client/LICENSE' }
  s.author = { 'Ryware' => 'contact@ryware.dev' }
  s.source = { :git => 'https://github.com/Ryware/nona-config.git', :tag => s.version.to_s }
  s.ios.deployment_target = '15.0'
  s.osx.deployment_target = '12.0'
  s.swift_versions = ['5.9', '6.0']
  s.source_files = 'client/swift/Sources/NonaClient/**/*.swift'
  s.frameworks = 'Foundation', 'CryptoKit'
end
